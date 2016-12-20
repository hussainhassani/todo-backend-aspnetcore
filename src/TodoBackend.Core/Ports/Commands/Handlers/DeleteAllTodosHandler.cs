using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using TodoBackend.Core.BrighterFix;
using TodoBackend.Core.Ports.Commands.Messages;

namespace TodoBackend.Core.Ports.Commands.Handlers
{
    public sealed class DeleteAllTodosHandler : RequestHandlerAsync<DeleteAllTodos>
    {
        private readonly DummyRepository _repository;

        public DeleteAllTodosHandler(DummyRepository repository)
        {
            _repository = repository;
        }

        [RequestLoggingAsync2(1, HandlerTiming.Before)]
        public override async Task<DeleteAllTodos> HandleAsync(DeleteAllTodos command, CancellationToken? ct = null)
        {
            await _repository.ClearAsync();

            return await base.HandleAsync(command, ct);
        }
    }
}