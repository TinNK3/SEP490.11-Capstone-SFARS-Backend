using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Notification;
using SFARS.Application.Services;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Interfaces;
using SFARS.Domain.Specifications.Params;
using SFARS.Infrastructure.Hubs;
using System.Linq.Expressions;

namespace SFARS.Tests.Application.Services.Notification
{

    /// <summary>
    /// Unit tests for the Notification system: SendNotification, GetNotifications, MarkAsRead, MarkAllAsRead, GetUnreadCount
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly Mock<ISystemMessageService> _msgServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IHubContext<NotificationHub>> _hubContextMock;
        private readonly Mock<ILogger<NotificationService>> _loggerMock;
        private readonly Mock<IGenericRepository<NotificationLog, Guid>> _notificationRepoMock;
        private readonly Mock<IHubClients> _hubClientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly Mock<IFcmPushService> _fcmPushServiceMock;
        private readonly NotificationService _sut;

        // Shared test data
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly Guid _testUserId2 = Guid.NewGuid();

        public NotificationServiceTests()
        {
            _msgServiceMock = new Mock<ISystemMessageService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _hubContextMock = new Mock<IHubContext<NotificationHub>>();
            _loggerMock = new Mock<ILogger<NotificationService>>();
            _notificationRepoMock = new Mock<IGenericRepository<NotificationLog, Guid>>();
            _fcmPushServiceMock = new Mock<IFcmPushService>();

            // Setup SignalR mock chain: HubContext → Clients → User(id) → IClientProxy
            _hubClientsMock = new Mock<IHubClients>();
            _clientProxyMock = new Mock<IClientProxy>();

            _hubContextMock.Setup(x => x.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(x => x.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);

            _clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Setup default message service
            _msgServiceMock.Setup(x => x.GetMessageAsync(It.IsAny<string>()))
                .ReturnsAsync((string msgId) => $"Message for {msgId}");

            // Setup UnitOfWork to return the NotificationLog repository mock
            _unitOfWorkMock.Setup(x => x.Repository<NotificationLog, Guid>())
                .Returns(_notificationRepoMock.Object);

            // Default: SaveChangesAsync succeeds
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);

            _sut = new NotificationService(
                _msgServiceMock.Object,
                _unitOfWorkMock.Object,
                _hubContextMock.Object,
                _loggerMock.Object,
                _fcmPushServiceMock.Object);
        }

        #region SendNotificationAsync Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationAsync delegates to SendNotificationsAsync with single userId
        /// Precondition: Valid userId, title, message, and type
        /// Expected Result: NotificationLog saved, SignalR events sent
        /// </summary>
        [Fact]
        public async Task SendNotificationAsync_ValidInput_DelegatesToBatchMethod()
        {
            // Arrange
            var title = "Test Title";
            var message = "Test Message";

            // Act
            var result = await _sut.SendNotificationAsync(_testUserId, title, message, NotificationType.Alert);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _notificationRepoMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<NotificationLog>>(
                logs => logs.Count() == 1 && logs.First().UserId == _testUserId)), Times.Once);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
            _fcmPushServiceMock.Verify(x => x.SendToUsersAsync(It.IsAny<IEnumerable<Guid>>(), title, message, It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        #endregion

        #region SendNotificationsAsync Tests

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: SendNotificationsAsync with empty user list
        /// Precondition: Empty IEnumerable of Guids passed
        /// Expected Result: Returns warning SYS_Warning0001, no DB save or SignalR dispatch
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_EmptyUserList_ReturnsWarning()
        {
            // Arrange & Act
            var result = await _sut.SendNotificationsAsync(
                Enumerable.Empty<Guid>(), "Title", "Message", NotificationType.System);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Warning0001);
            _notificationRepoMock.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync with single user saves NotificationLog correctly
        /// Precondition: One valid userId
        /// Expected Result: Exactly 1 NotificationLog saved with correct fields
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_SingleUser_SavesCorrectNotificationLog()
        {
            // Arrange
            NotificationLog? capturedLog = null;
            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback<IEnumerable<NotificationLog>>(logs => capturedLog = logs.First())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "⚠️ Cảnh báo", "Khu vực nguy hiểm", NotificationType.Alert);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            capturedLog.Should().NotBeNull();
            capturedLog!.UserId.Should().Be(_testUserId);
            capturedLog.Title.Should().Be("⚠️ Cảnh báo");
            capturedLog.Message.Should().Be("Khu vực nguy hiểm");
            capturedLog.Type.Should().Be(NotificationType.Alert);
            capturedLog.IsRead.Should().BeFalse();
            capturedLog.ReferenceId.Should().BeNull();
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync with multiple users saves all NotificationLogs
        /// Precondition: Two distinct userIds
        /// Expected Result: 2 NotificationLogs saved, each with correct UserId
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_MultipleUsers_SavesAllNotificationLogs()
        {
            // Arrange
            List<NotificationLog>? capturedLogs = null;
            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback<IEnumerable<NotificationLog>>(logs => capturedLogs = logs.ToList())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SendNotificationsAsync(
                new[] { _testUserId, _testUserId2 }, "Title", "Body", NotificationType.SosDispatch);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            capturedLogs.Should().HaveCount(2);
            capturedLogs!.Select(l => l.UserId).Should().Contain(new[] { _testUserId, _testUserId2 });
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: SendNotificationsAsync deduplicates userIds
        /// Precondition: Same userId passed twice
        /// Expected Result: Only 1 NotificationLog saved (duplicate removed)
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_DuplicateUserIds_DeduplicatesBeforeSaving()
        {
            // Arrange
            List<NotificationLog>? capturedLogs = null;
            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback<IEnumerable<NotificationLog>>(logs => capturedLogs = logs.ToList())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SendNotificationsAsync(
                new[] { _testUserId, _testUserId, _testUserId }, "Title", "Body", NotificationType.System);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            capturedLogs.Should().HaveCount(1);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync with ReferenceId stores it correctly
        /// Precondition: Valid referenceId (e.g. incident ID)
        /// Expected Result: NotificationLog.ReferenceId matches the input
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_WithReferenceId_StoresReferenceCorrectly()
        {
            // Arrange
            var referenceId = Guid.NewGuid();
            NotificationLog? capturedLog = null;
            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback<IEnumerable<NotificationLog>>(logs => capturedLog = logs.First())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "Title", "Body", NotificationType.Mission, referenceId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            capturedLog!.ReferenceId.Should().Be(referenceId);
            capturedLog.Type.Should().Be(NotificationType.Mission);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync dispatches SignalR ReceiveNotification event
        /// Precondition: Valid single user request
        /// Expected Result: SignalR sends "ReceiveNotification" to correct userId
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_DispatchesSignalR_ReceiveNotification()
        {
            // Arrange & Act
            await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "Signal Title", "Signal Body", NotificationType.System);

            // Assert
            _hubClientsMock.Verify(x => x.User(_testUserId.ToString()), Times.AtLeastOnce);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object?[]>(args => args.Length == 1 && args[0] is NotificationDto),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync dispatches SignalR IncrementUnreadCount event
        /// Precondition: Valid single user request
        /// Expected Result: SignalR sends "IncrementUnreadCount" to correct userId
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_DispatchesSignalR_IncrementUnreadCount()
        {
            // Arrange & Act
            await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "Title", "Body", NotificationType.Alert);

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "IncrementUnreadCount",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync sends SignalR events for each user in parallel
        /// Precondition: Two distinct users
        /// Expected Result: ReceiveNotification called twice, IncrementUnreadCount called twice
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_MultipleUsers_SignalRSentForEachUser()
        {
            // Arrange & Act
            await _sut.SendNotificationsAsync(
                new[] { _testUserId, _testUserId2 }, "Title", "Body", NotificationType.Alert);

            // Assert — 2 ReceiveNotification + 2 IncrementUnreadCount = 4 calls total
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "ReceiveNotification",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "IncrementUnreadCount",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync execution order: DB → SignalR
        /// Precondition: Valid request
        /// Expected Result: AddRangeAsync and SaveChangesAsync called before SignalR
        /// </summary>
        [Fact]
        public async Task SendNotificationsAsync_ExecutionOrder_DbBeforeSignalR()
        {
            // Arrange
            var callOrder = new List<string>();

            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback(() => callOrder.Add("DB_AddRange"))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(x => x.SaveChangesAsync())
                .Callback(() => callOrder.Add("DB_Save"))
                .ReturnsAsync(1);

            _clientProxyMock
                .Setup(x => x.SendCoreAsync("ReceiveNotification", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("SignalR"))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "Title", "Body", NotificationType.System);

            // Assert — DB must complete before SignalR
            callOrder.IndexOf("DB_AddRange").Should().BeLessThan(callOrder.IndexOf("DB_Save"));
            callOrder.IndexOf("DB_Save").Should().BeLessThan(callOrder.IndexOf("SignalR"));
        }

        #endregion

        #region GetNotificationsAsync Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: GetNotificationsAsync when user has no notifications
        /// Precondition: CountAsync returns 0
        /// Expected Result: Returns empty paginated result with totalItems = 0
        /// </summary>
        [Fact]
        public async Task GetNotificationsAsync_NoNotifications_ReturnsEmptyPaginatedResult()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(0);

            var specParams = new NotificationSpecParams { Page = 1, PageSize = 10 };

            // Act
            var result = await _sut.GetNotificationsAsync(_testUserId, specParams);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paginated = result.Data.Should().BeOfType<PaginatedResultDto<NotificationDto>>().Subject;
            paginated.Items.Should().BeEmpty();
            paginated.Pagination.TotalItems.Should().Be(0);
            paginated.Pagination.TotalPages.Should().Be(0);

            // Verify: NO second query for data (short-circuit optimization)
            _notificationRepoMock.Verify(
                x => x.GetAllWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()),
                Times.Never);
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: GetNotificationsAsync returns paginated notifications with correct mapping
        /// Precondition: User has 1 notification in DB
        /// Expected Result: Returns paginated result with 1 item correctly mapped to NotificationDto
        /// </summary>
        [Fact]
        public async Task GetNotificationsAsync_WithNotifications_ReturnsMappedDtos()
        {
            // Arrange
            var notificationLog = CreateNotificationLog(_testUserId, "Test Title", "Test Message", NotificationType.Alert);

            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(1);

            _notificationRepoMock
                .Setup(x => x.GetAllWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<NotificationLog> { notificationLog });

            var specParams = new NotificationSpecParams { Page = 1, PageSize = 10 };

            // Act
            var result = await _sut.GetNotificationsAsync(_testUserId, specParams);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            var paginated = result.Data.Should().BeOfType<PaginatedResultDto<NotificationDto>>().Subject;
            paginated.Items.Should().HaveCount(1);

            var dto = paginated.Items.First();
            dto.Id.Should().Be(notificationLog.Id);
            dto.Title.Should().Be("Test Title");
            dto.Message.Should().Be("Test Message");
            dto.Type.Should().Be(NotificationType.Alert);
            dto.IsRead.Should().BeFalse();
        }

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: GetNotificationsAsync with IsRead filter
        /// Precondition: Filter IsRead = false
        /// Expected Result: CountAsync called with specification (filter applied)
        /// </summary>
        [Fact]
        public async Task GetNotificationsAsync_WithIsReadFilter_AppliesFilterToSpec()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(0);

            var specParams = new NotificationSpecParams { Page = 1, PageSize = 10, IsRead = false };

            // Act
            var result = await _sut.GetNotificationsAsync(_testUserId, specParams);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _notificationRepoMock.Verify(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()), Times.Once);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetNotificationsAsync calculates totalPages correctly
        /// Precondition: 25 notifications with pageSize = 10
        /// Expected Result: totalPages = 3 (ceil(25/10))
        /// </summary>
        [Fact]
        public async Task GetNotificationsAsync_CalculatesTotalPages_Correctly()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(25);

            var notifications = Enumerable.Range(0, 10)
                .Select(_ => CreateNotificationLog(_testUserId, "T", "M", NotificationType.System))
                .ToList();

            _notificationRepoMock
                .Setup(x => x.GetAllWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()))
                .ReturnsAsync(notifications);

            var specParams = new NotificationSpecParams { Page = 1, PageSize = 10 };

            // Act
            var result = await _sut.GetNotificationsAsync(_testUserId, specParams);

            // Assert
            var paginated = result.Data.Should().BeOfType<PaginatedResultDto<NotificationDto>>().Subject;
            paginated.Pagination.TotalPages.Should().Be(3);
            paginated.Pagination.TotalItems.Should().Be(25);
        }

        #endregion

        #region MarkAsReadAsync Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: MarkAsReadAsync marks unread notification as read
        /// Precondition: Unread notification exists for the user
        /// Expected Result: IsRead set to true, Update called, SignalR UpdateUnreadCount dispatched
        /// </summary>
        [Fact]
        public async Task MarkAsReadAsync_UnreadNotification_MarksAsReadAndUpdatesCount()
        {
            // Arrange
            var notification = CreateNotificationLog(_testUserId, "Title", "Body", NotificationType.Alert, isRead: false);

            _notificationRepoMock
                .Setup(x => x.GetWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()))
                .ReturnsAsync(notification);

            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(3); // 3 remaining unread

            // Act
            var result = await _sut.MarkAsReadAsync(notification.Id, _testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            notification.IsRead.Should().BeTrue();
            _notificationRepoMock.Verify(x => x.Update(notification), Times.Once);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);

            // Verify SignalR UpdateUnreadCount dispatched with correct count
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "UpdateUnreadCount",
                It.Is<object?[]>(args => args.Length == 1 && (int)args[0]! == 3),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test Type: ABNORMAL
        /// Tests: MarkAsReadAsync when notification doesn't exist
        /// Precondition: GetWithSpecAsync returns null
        /// Expected Result: Returns success (idempotent), no Update called, no SignalR
        /// </summary>
        [Fact]
        public async Task MarkAsReadAsync_NotificationNotFound_ReturnsSuccessNoUpdate()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.GetWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()))
                .ReturnsAsync((NotificationLog?)null);

            // Act
            var result = await _sut.MarkAsReadAsync(Guid.NewGuid(), _testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _notificationRepoMock.Verify(x => x.Update(It.IsAny<NotificationLog>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: MarkAsReadAsync when notification is already read
        /// Precondition: Notification exists with IsRead = true
        /// Expected Result: Returns success (idempotent), no Update called, no SignalR
        /// </summary>
        [Fact]
        public async Task MarkAsReadAsync_AlreadyRead_ReturnsSuccessNoUpdate()
        {
            // Arrange
            var notification = CreateNotificationLog(_testUserId, "Title", "Body", NotificationType.System, isRead: true);

            _notificationRepoMock
                .Setup(x => x.GetWithSpecAsync(It.IsAny<ISpecification<NotificationLog>>(), It.IsAny<bool>()))
                .ReturnsAsync(notification);

            // Act
            var result = await _sut.MarkAsReadAsync(notification.Id, _testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _notificationRepoMock.Verify(x => x.Update(It.IsAny<NotificationLog>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        #endregion

        #region MarkAllAsReadAsync Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: MarkAllAsReadAsync bulk updates all unread notifications
        /// Precondition: User has unread notifications
        /// Expected Result: UpdateWithSpecAsync called, SignalR UpdateUnreadCount(0) dispatched
        /// </summary>
        [Fact]
        public async Task MarkAllAsReadAsync_HasUnread_BulkUpdatesAndSendsSignalR()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.UpdateWithSpecAsync(
                    It.IsAny<ISpecification<NotificationLog>>(),
                    It.IsAny<Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<NotificationLog>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<NotificationLog>>>>()))
                .ReturnsAsync(5); // 5 rows affected

            // Act
            var result = await _sut.MarkAllAsReadAsync(_testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "UpdateUnreadCount",
                It.Is<object?[]>(args => args.Length == 1 && (int)args[0]! == 0),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: MarkAllAsReadAsync when no unread notifications exist
        /// Precondition: User has 0 unread notifications
        /// Expected Result: Returns success, NO SignalR event dispatched (unnecessary)
        /// </summary>
        [Fact]
        public async Task MarkAllAsReadAsync_NoUnread_ReturnsSuccessNoSignalR()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.UpdateWithSpecAsync(
                    It.IsAny<ISpecification<NotificationLog>>(),
                    It.IsAny<Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<NotificationLog>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<NotificationLog>>>>()))
                .ReturnsAsync(0); // 0 rows affected

            // Act
            var result = await _sut.MarkAllAsReadAsync(_testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "UpdateUnreadCount",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region GetUnreadCountAsync Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: GetUnreadCountAsync returns correct unread count
        /// Precondition: User has 7 unread notifications
        /// Expected Result: Returns success with data = 7
        /// </summary>
        [Fact]
        public async Task GetUnreadCountAsync_HasUnread_ReturnsCorrectCount()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(7);

            // Act
            var result = await _sut.GetUnreadCountAsync(_testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().Be(7);
        }

        /// <summary>
        /// Test Type: BOUNDARY
        /// Tests: GetUnreadCountAsync when user has zero unread notifications
        /// Precondition: User has 0 unread notifications
        /// Expected Result: Returns success with data = 0
        /// </summary>
        [Fact]
        public async Task GetUnreadCountAsync_NoUnread_ReturnsZero()
        {
            // Arrange
            _notificationRepoMock
                .Setup(x => x.CountAsync(It.IsAny<ISpecification<NotificationLog>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _sut.GetUnreadCountAsync(_testUserId);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            result.Data.Should().Be(0);
        }

        #endregion

        #region NotificationType Coverage Tests

        /// <summary>
        /// Test Type: NORMAL
        /// Tests: SendNotificationsAsync handles all NotificationType enum values correctly
        /// Precondition: Each NotificationType value passed
        /// Expected Result: NotificationLog.Type matches input for all enum values
        /// </summary>
        [Theory]
        [InlineData(NotificationType.System)]
        [InlineData(NotificationType.Mission)]
        [InlineData(NotificationType.Alert)]
        [InlineData(NotificationType.SosDispatch)]
        public async Task SendNotificationsAsync_AllNotificationTypes_SavedCorrectly(NotificationType type)
        {
            // Arrange
            NotificationLog? capturedLog = null;
            _notificationRepoMock
                .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<NotificationLog>>()))
                .Callback<IEnumerable<NotificationLog>>(logs => capturedLog = logs.First())
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SendNotificationsAsync(
                new[] { _testUserId }, "Title", "Body", type);

            // Assert
            result.ResultCode.Should().Be(ResultCodeConst.SYS_Success0002);
            capturedLog!.Type.Should().Be(type);
        }

        #endregion

        #region Helper Methods

        private NotificationLog CreateNotificationLog(
            Guid userId,
            string title,
            string message,
            NotificationType type,
            bool isRead = false,
            Guid? referenceId = null)
        {
            return new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = isRead,
                ReferenceId = referenceId,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
        }

        #endregion
    }
}
