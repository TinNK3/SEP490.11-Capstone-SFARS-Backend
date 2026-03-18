using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Faq;
using SFARS.Application.Dtos.Faq;
using SFARS.Domain.Common.Constants;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications.Params;

namespace SFARS.API.Controller
{
    /// <summary>
    /// FAQ endpoints (public read + admin management routes)
    /// </summary>
    [ApiController]
    public class FaqController : ControllerBase
    {
        private readonly IFaqService<FaqDto> _faqService;

        public FaqController(IFaqService<FaqDto> faqService)
        {
            _faqService = faqService;
        }

        #region Public Read

        /// <summary>
        /// [Public] Get all active FAQs (IsActive = true), sorted by Order
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.Faq.GetAll, Name = "Faq_GetAllActive")]
        public async Task<IActionResult> GetAllActiveAsync()
        {
            var result = await _faqService.GetAllActiveFaqsAsync();
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Public] Get a single FAQ by ID
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.Faq.GetById, Name = "Faq_GetById")]
        public async Task<IActionResult> GetPublicByIdAsync(Guid id)
        {
            var result = await _faqService.GetFaqByIdAsync(id);
            return this.ToIActionResult(result);
        }

        #endregion

        #region Admin Manage FAQs

        /// <summary>
        /// [Admin] Get all FAQs with pagination (including inactive)
        /// </summary>
        /// <remarks>
        /// **Pagination:**
        /// - `page` (1-based, default: 1)
        /// - `limit` (default: 10)
        /// </remarks>
        [Authorize]
        [HttpGet(APIRoute.Admin.GetAllFaqs, Name = "AdminFaq_GetAllPaginated")]
        public async Task<IActionResult> GetAllPaginatedAsync([FromQuery] BaseSpecParams specParams)
        {
            var result = await _faqService.GetAllFaqsPaginatedAsync(specParams);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Get a single FAQ by ID
        /// </summary>
        [Authorize]
        [HttpGet(APIRoute.Admin.GetFaqById, Name = "AdminFaq_GetById")]
        public async Task<IActionResult> GetAdminByIdAsync(Guid id)
        {
            var result = await _faqService.GetFaqByIdAsync(id);
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Admin] Create a new FAQ
        /// </summary>
        [Authorize(Roles = UserTypeConstants.Admin)]
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
        [Authorize(Roles = UserTypeConstants.Admin)]
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
        [Authorize(Roles = UserTypeConstants.Admin)]
        [HttpDelete(APIRoute.Admin.DeleteFaq, Name = "AdminFaq_Delete")]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var result = await _faqService.DeleteFaqAsync(id);
            return this.ToIActionResult(result);
        }

        #endregion
    }
}
