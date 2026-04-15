using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Dtos.Community;
using SFARS.Application.Interfaces.Services;
using SFARS.Domain.Common.Constants;

namespace SFARS.API.Controller;

[ApiController]
[Authorize]
public class QuizController : ControllerBase
{
    private readonly IQuizService _svc;

    public QuizController(IQuizService svc) => _svc = svc;

    #region User Endpoints

    /// <summary>Danh sách các bộ Quiz có sẵn (đang Active)</summary>
    [HttpGet(APIRoute.Quiz.GetList, Name = nameof(GetQuizzesAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetQuizzesAsync()
        => this.ToIActionResult(await _svc.GetQuizzesAsync());

    /// <summary>Lấy dữ liệu để chơi Game (kèm câu hỏi và xáo trộn options)</summary>
    [HttpGet(APIRoute.Quiz.GetForGame, Name = nameof(GetQuizForGameAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> GetQuizForGameAsync(Guid id)
        => this.ToIActionResult(await _svc.GetQuizForGameAsync(id));

    /// <summary>Nộp kết quả bài thi và nhận thưởng</summary>
    [HttpPost(APIRoute.Quiz.Submit, Name = nameof(SubmitQuizAsync))]
    public async Task<IActionResult> SubmitQuizAsync([FromBody] QuizSubmitRequest req)
        => this.ToIActionResult(await _svc.SubmitQuizAsync(User.GetUserId(), req));

    /// <summary>Xem lịch sử làm bài của bản thân</summary>
    [HttpGet(APIRoute.Quiz.GetMyHistory, Name = nameof(GetMyHistoryAsync))]
    public async Task<IActionResult> GetMyHistoryAsync()
        => this.ToIActionResult(await _svc.GetUserHistoryAsync(User.GetUserId()));

    /// <summary>Xem chi tiết kết quả một lần làm bài (User)</summary>
    [HttpGet(APIRoute.Quiz.GetHistoryDetail, Name = nameof(GetHistoryDetailAsync))]
    public async Task<IActionResult> GetHistoryDetailAsync(Guid id)
        => this.ToIActionResult(await _svc.GetHistoryDetailAsync(id, User.GetUserId()));

    #endregion

    #region Admin Endpoints

    /// <summary>Lọc tất cả Quiz cho Admin (bao gồm cả Inactive)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Admin.GetAllQuizzes, Name = nameof(GetAllQuizzesAdminAsync))]
    public async Task<IActionResult> GetAllQuizzesAdminAsync()
        => this.ToIActionResult(await _svc.GetAllQuizzesAdminAsync());

    /// <summary>Tạo bộ Quiz mới (Admin)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPost(APIRoute.Admin.CreateQuiz, Name = nameof(CreateQuizAsync))]
    public async Task<IActionResult> CreateQuizAsync([FromBody] QuizManageDto dto)
        => this.ToIActionResult(await _svc.CreateQuizAsync(dto));

    /// <summary>Cập nhật bộ Quiz (Admin)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpPut(APIRoute.Admin.UpdateQuiz, Name = nameof(UpdateQuizAsync))]
    public async Task<IActionResult> UpdateQuizAsync(Guid id, [FromBody] QuizManageDto dto)
        => this.ToIActionResult(await _svc.UpdateQuizAsync(id, dto));

    /// <summary>Xoá bộ Quiz (Admin)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpDelete(APIRoute.Admin.DeleteQuiz, Name = nameof(DeleteQuizAsync))]
    public async Task<IActionResult> DeleteQuizAsync(Guid id)
        => this.ToIActionResult(await _svc.DeleteQuizAsync(id));

    /// <summary>Xem toàn bộ lịch sử làm bài của các User (Admin)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Admin.GetQuizAttempts, Name = nameof(GetAllHistoryAdminAsync))]
    public async Task<IActionResult> GetAllHistoryAdminAsync()
        => this.ToIActionResult(await _svc.GetAllHistoryAdminAsync());

    /// <summary>Xem chi tiết một lượt làm bài của bất kỳ User nào (Admin)</summary>
    [Authorize(Roles = UserTypeConstants.Admin)]
    [HttpGet(APIRoute.Admin.GetAttemptDetail, Name = nameof(GetAttemptDetailAdminAsync))]
    public async Task<IActionResult> GetAttemptDetailAdminAsync(Guid id)
        => this.ToIActionResult(await _svc.GetHistoryDetailAsync(id));

    #endregion
}
