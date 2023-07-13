using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace AspNetCoreMinimalAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class TodosController : ControllerBase
{
    private static ConcurrentDictionary<Guid, TodoItem> _todos = new ConcurrentDictionary<Guid, TodoItem>();

    private readonly ILogger<TodosController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Tracer _tracer;

    public TodosController(ILoggerFactory loggerFactory, Tracer tracer)
    {
        _logger = loggerFactory.CreateLogger<TodosController>();
        _loggerFactory = loggerFactory;
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
        using (var span = _tracer.StartActiveSpan("NewTodo"))
        {
            item.Id = Guid.NewGuid();
            _todos.TryAdd(item.Id, item);
            span.SetAttribute("itemId", item.Id.ToString());
        }

        _logger.LogInformation("New item added with id {id}", item.Id);

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
