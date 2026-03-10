using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extensions;
using SFARS.API.Payloads;
using SFARS.Application.Dtos.Faq;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller
{
    /// <summary>
    /// Public FAQ endpoints - Anonymous access for reading FAQs
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    public class FaqController : ControllerBase
    {
        private readonly IFaqService<FaqDto> _faqService;

        public FaqController(IFaqService<FaqDto> faqService)
        {
            _faqService = faqService;
        }

        /// <summary>
        /// [Public] Get all active FAQs (IsActive = true), sorted by Order
        /// </summary>
        [HttpGet(APIRoute.Faq.GetAll, Name = "Faq_GetAllActive")]
        public async Task<IActionResult> GetAllActiveAsync()
        {
            var result = await _faqService.GetAllActiveFaqsAsync();
            return this.ToIActionResult(result);
        }

        /// <summary>
        /// [Public] Get a single FAQ by ID
        /// </summary>
        [HttpGet(APIRoute.Faq.GetById, Name = "Faq_GetById")]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            var result = await _faqService.GetFaqByIdAsync(id);
            return this.ToIActionResult(result);
        }
    }
}
