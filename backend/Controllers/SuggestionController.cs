using Cleanarr.Data;
using Cleanarr.Models;
using Cleanarr.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanarr.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuggestionController : ControllerBase
{
    private readonly CleanarrDbContext _db;
    private readonly SuggestionService _suggestionService;

    public SuggestionController(CleanarrDbContext db, SuggestionService suggestionService)
    {
        _db = db;
        _suggestionService = suggestionService;
    }

    // GET /api/suggestion
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Suggestion>>> GetSuggestions()
    {
        var suggestions = await _db.Suggestions
            .Where(s => !s.Dismissed)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
        
        return Ok(suggestions);
    }

    // GET /api/suggestion/rules
    [HttpGet("rules")]
    public async Task<ActionResult<IEnumerable<SuggestionRule>>> GetRules()
    {
        return await _db.SuggestionRules.OrderBy(r => r.Id).ToListAsync();
    }

    // POST /api/suggestion/rules
    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] SuggestionRule rule)
    {
        rule.Id = 0; // Let database assign ID
        rule.IsCustom = true;
        
        _db.SuggestionRules.Add(rule);
        await _db.SaveChangesAsync();
        
        // Regenerate suggestions
        await _suggestionService.GenerateSuggestionsAsync();
        
        return Ok(rule);
    }

    // PUT /api/suggestion/rules/{id}
    [HttpPut("rules/{id}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] SuggestionRule rule)
    {
        var existing = await _db.SuggestionRules.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Name = rule.Name;
        existing.Description = rule.Description;
        existing.Enabled = rule.Enabled;
        existing.ApplyToMovies = rule.ApplyToMovies;
        existing.ApplyToSeries = rule.ApplyToSeries;
        existing.ConditionsJson = rule.ConditionsJson;

        await _db.SaveChangesAsync();
        
        // Regenerate suggestions with new rules
        await _suggestionService.GenerateSuggestionsAsync();
        
        return Ok(existing);
    }

    // DELETE /api/suggestion/rules/{id}
    [HttpDelete("rules/{id}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var rule = await _db.SuggestionRules.FindAsync(id);
        if (rule == null)
            return NotFound();

        // Only allow deleting custom rules
        if (!rule.IsCustom)
            return BadRequest(new { message = "Cannot delete default rules" });

        _db.SuggestionRules.Remove(rule);
        await _db.SaveChangesAsync();
        
        // Regenerate suggestions
        await _suggestionService.GenerateSuggestionsAsync();
        
        return Ok(new { message = "Rule deleted" });
    }

    // POST /api/suggestion/generate
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateSuggestions()
    {
        await _suggestionService.GenerateSuggestionsAsync();
        return Ok(new { message = "Suggestions generated" });
    }

    // POST /api/suggestion/{id}/dismiss
    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> DismissSuggestion(int id)
    {
        var suggestion = await _db.Suggestions.FindAsync(id);
        if (suggestion == null)
            return NotFound();

        suggestion.Dismissed = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Suggestion dismissed" });
    }

    // DELETE /api/suggestion/{id}/execute
    [HttpDelete("{id}/execute")]
    public async Task<IActionResult> ExecuteSuggestion(int id)
    {
        var suggestion = await _db.Suggestions.FindAsync(id);
        if (suggestion == null)
            return NotFound();

        // Mark as dismissed (we don't actually delete, just track it was acted upon)
        suggestion.Dismissed = true;
        await _db.SaveChangesAsync();

        // Return the media info so the frontend can call the delete endpoint
        return Ok(new 
        { 
            mediaType = suggestion.MediaType,
            mediaId = suggestion.MediaId,
            message = "Ready to delete"
        });
    }
}
