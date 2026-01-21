using Microsoft.AspNetCore.Mvc;
using SFARS.API.Extension;
using SFARS.API.Payloads;
using SFARS.API.Payloads.Request.Snake;
using SFARS.Application.Dtos;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.API.Controller;

[ApiController]
public class SnakeController : ControllerBase
{
    private readonly ISnakeService<SnakeDto> _snakeService;

    public SnakeController(ISnakeService<SnakeDto> snakeService)
    {
        _snakeService = snakeService;
    }

    /// <summary>
    /// Retrieves all snake entities asynchronously.
    /// </summary>
    /// <returns>An IActionResult containing the list of all snakes.</returns>
    [HttpGet(APIRoute.Snake.GetAll, Name= nameof(GetAllAsync))]
    public async Task<IActionResult> GetAllAsync()
    {
       return Ok(await _snakeService.GetAllAsync());
    }

    /// <summary>
    /// Retrieves a snake by its ID
    /// </summary>
    [HttpGet(APIRoute.Snake.GetById, Name = "GetSnakeById")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        return Ok(await _snakeService.GetSnakeById(id));
    }

    /// <summary>
    /// Search snakes with pagination
    /// </summary>
    [HttpGet(APIRoute.Snake.Search)]
    public async Task<IActionResult> SearchAsync([FromQuery] string? term, [FromQuery] int page = 0, [FromQuery] int size = 10)
    {
        return Ok(await _snakeService.SearchSnakes(term, page, size));
    }

    /// <summary>
    /// Creates a new snake entity asynchronously.
    /// </summary>
    [HttpPost(APIRoute.Snake.Create, Name= nameof(CreateAsync))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSnakeRequest req)
    {
       return Ok(await _snakeService.CreateSnake(req.ToSnake()));
    }

    /// <summary>
    /// Updates an existing snake entity asynchronously.
    /// </summary>
    [HttpPut(APIRoute.Snake.Update, Name= nameof(UpdateAsync))]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateSnakeRequest req)
    {
       return Ok(await _snakeService.UpdateAsync(id, req.ToSnakeForUpdate()));
    }

    /// <summary>
    /// Deletes a snake entity asynchronously.
    /// </summary>
    [HttpDelete(APIRoute.Snake.Delete, Name = "DeleteSnake")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        return Ok(await _snakeService.DeleteSnake(id));
    }
}
