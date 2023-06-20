using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;

namespace AspNetCoreMinimalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class TodosController : ControllerBase
{
    private static ConcurrentDictionary<Guid, TodoItem> _todos = new ConcurrentDictionary<Guid, TodoItem>();

    private readonly ILogger<TodosController> _logger;
    private readonly Tracer _tracer;

    public TodosController(ILogger<TodosController> logger, Tracer tracer)
    {
        _logger = logger;
        _tracer = tracer;
    }

    [HttpGet]
    public ActionResult<IEnumerable<TodoItem>> GetAll()
    {
        return Ok(_todos.Values);
    }

    [HttpPost]
    public ActionResult<TodoItem> Post(TodoItem item)
    {
        item.Id = Guid.NewGuid();
        _todos.TryAdd(item.Id, item);

        _logger.LogInformation("New item added with id {id}", item.Id);
        Tracer.CurrentSpan.SetAttribute("ItemId", item.Id.ToString());

        var result = new ObjectResult(item);
        result.StatusCode = StatusCodes.Status201Created;
        return result;
    }

    [HttpGet]
    [Route("{id}")]
    public ActionResult<TodoItem> GetOne(Guid id)
    {
        if (!_todos.TryGetValue(id, out var todo))
        {
            return NotFound();
        }

        return Ok(todo);
    }

    [HttpDelete]
    [Route("{id}")]
    public ActionResult Delete(Guid id)
    {
        if(!_todos.TryRemove(id, out _))
        {
            _logger.LogWarning("Todo with id {id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Todo with id {id} deleted", id);

        return Ok();
    }
}

public class TodoItem
{
    public Guid Id { get; set; }
    public DateTime DueDate { get; set; }
    public string Text { get; set; }
    public bool Done { get; set; }
}
