using GenZStore.Commands;
using GenZStore.Queries;
using GenZStore.Models;
using Microsoft.AspNetCore.Mvc;

namespace GenZStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryCommand _categoryCommand;
        private readonly ICategoryQuery _categoryQuery;

        public CategoriesController(ICategoryCommand categoryCommand, ICategoryQuery categoryQuery)
        {
            _categoryCommand = categoryCommand;
            _categoryQuery = categoryQuery;
        }

        // ✅ Create category
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            var id = await _categoryCommand.CreateCategoryAsync(dto);
            return Ok(new { CategoryId = id });
        }

        // ✅ Update category
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryDto dto)
        {
            var success = await _categoryCommand.UpdateCategoryAsync(id, dto);
            return success ? Ok(new { Message = "Updated successfully" }) : NotFound();
        }

        // ✅ Delete category
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var success = await _categoryCommand.DeleteCategoryAsync(id);
            return success ? Ok(new { Message = "Deleted successfully" }) : NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCategories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await _categoryQuery.GetAllAsync(page, pageSize, search);
            return Ok(result);
        }

        // ✅ Get category by Id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoryById(Guid id)
        {
            var category = await _categoryQuery.GetByIdAsync(id);
            return category == null ? NotFound() : Ok(category);
        }

        // ✅ Search category by name
        
    }
}