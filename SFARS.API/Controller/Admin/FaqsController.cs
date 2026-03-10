using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Faq;
using SFARS.Application.Dtos.Faq;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller.Admin
{
    /// <summary>
    /// Admin — FAQ Management (api/admin/faqs)
    /// </summary>
    [ApiController]
    [Authorize(Roles = UserTypeConstants.Admin)]
    public class FaqsController : ControllerBase
    {
        private readonly IFaqService<FaqDto> _faqService;

        public FaqsController(IFaqService<FaqDto> faqService)
        {
            _faqService = faqService;
        }

        /// <summary>
        /// [Admin] Get all FAQs with pagination (including inactive)
        /// </summary>
        /// <param name="pageIndex">Zero-based page index (default: 0)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        [HttpGet(APIRoute.Admin.GetAllFaqs, Name = "AdminFaq_GetAllPaginated")]
        public async Task<IActionResult> GetAllPaginatedAsync(
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10)
        {
            var result = await _faqService.GetAllFaqsPaginatedAsync(pageIndex, pageSize);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get a single FAQ by ID
        /// </summary>
        [HttpGet(APIRoute.Admin.GetFaqById, Name = "AdminFaq_GetById")]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            var result = await _faqService.GetFaqByIdAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Create a new FAQ
        /// </summary>
        [HttpPost(APIRoute.Admin.CreateFaq, Name = "AdminFaq_Create")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateFaqRequest req)
        {
            var dto = req.ToFaqDto();
            var result = await _faqService.CreateFaqAsync(dto);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Update an existing FAQ
        /// </summary>
        [HttpPut(APIRoute.Admin.UpdateFaq, Name = "AdminFaq_Update")]
        public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateFaqRequest req)
        {
            var dto = req.ToFaqDto();
            var result = await _faqService.UpdateFaqAsync(id, dto);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Delete (soft delete) a FAQ by setting IsActive = false
        /// </summary>
        [HttpDelete(APIRoute.Admin.DeleteFaq, Name = "AdminFaq_Delete")]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var result = await _faqService.DeleteFaqAsync(id);
            return this.ToIActionResult(result);
        }
    }
}
