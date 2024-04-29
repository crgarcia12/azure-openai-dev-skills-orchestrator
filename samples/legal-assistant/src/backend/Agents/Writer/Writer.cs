#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
using Marketing.Hubs;
using Microsoft.AI.Agents.Abstractions;
using Microsoft.AI.Agents.Orleans;
using Microsoft.AI.DevTeam.Events;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Orleans.Runtime;

namespace Microsoft.AI.DevTeam;

[ImplicitStreamSubscription(Consts.OrleansNamespace)]
public class Writer : AiAgent<WriterState>, IWriter
{
    protected override string Namespace => Consts.OrleansNamespace;
    
    private readonly ILogger<CommunityManager> _logger;

    public Writer([PersistentState("state", "messages")] IPersistentState<AgentState<WriterState>> state, Kernel kernel, ISemanticTextMemory memory, ILogger<CommunityManager> logger) 
    : base(state, memory, kernel)
    {
        _logger = logger;
        if(state.State.Data == null)
        {
            state.State.Data = new WriterState();
        }
    }

    public async override Task HandleEvent(Event item)
    {
        switch (item.Type)
        {
            case nameof(EventTypes.UserChatInput):                
                //var lastCode = _state.State.History.Last().Message;

                _logger.LogInformation($"[{nameof(CommunityManager)}] Event {nameof(EventTypes.UserChatInput)}. UserMessage: {item.Message}");
                    
                var context = new KernelArguments { ["input"] = AppendChatHistory(item.Message) };
                string newArticle = await CallFunction(WriterPrompts.Write, context);
                _state.State.Data.WrittenArticle = newArticle;


                await PublishEvent(Consts.OrleansNamespace, this.GetPrimaryKeyString(), new Event
                {
                    Type = nameof(EventTypes.ArticleWritten),
                    Data = new Dictionary<string, string> {
                            { "UserId", item.Data["UserId"] },
                            { "UserMessage", item.Data["userMessage"] },
                            { "WrittenArticle", newArticle }
                        },
                    Message = newArticle
                });

                ArticleHub._allHubs.TryGetValue(item.Data["UserId"], out var articleHub);
                await articleHub.SendMessageToSpecificClient(item.Data["UserId"], newArticle);

                break;
            default:
                break;
        }
    }

    public Task<String> GetArticle()
    {
        return Task.FromResult(_state.State.Data.WrittenArticle);
    }
}

public interface IWriter : IGrainWithStringKey
{
    Task<String> GetArticle();
}

[GenerateSerializer]
public class WriterState
{
    [Id(0)]
    public string WrittenArticle { get; set; }
}

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task