using Moq;
using SFARS.Application.Services;
using Xunit;

namespace SFARS.Tests.Application.Services.Incidents
{
    public class IncidentSmsWebhookTests
    {
        [Theory]
        [InlineData("+84901234567", "0901234567")]
        [InlineData("84901234567", "0901234567")]
        [InlineData("0901234567", "0901234567")]
        [InlineData(" +84 901 234 567 ", "0901234567")]
        [InlineData("090-123-4567", "0901234567")]
        public void NormalizeVnPhoneNumber_Should_StandardizeFormat(string input, string expected)
        {
            // Act
            var result = IncidentService.NormalizeVnPhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}