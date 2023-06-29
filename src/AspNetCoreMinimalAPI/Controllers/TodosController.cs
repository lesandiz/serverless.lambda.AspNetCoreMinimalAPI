using Microsoft.AspNetCore.Mvc;
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
    private readonly Tracer _tracer;
    private readonly Meter _meter;
    private readonly Counter<int> _newTodoCounter;

    public TodosController(ILogger<TodosController> logger, Tracer tracer, Meter meter)
    {
        _logger = logger;
        _tracer = tracer;
        _meter = meter;
        _newTodoCounter = _meter.CreateCounter<int>("NewTodo");
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

        _newTodoCounter.Add(1);

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
