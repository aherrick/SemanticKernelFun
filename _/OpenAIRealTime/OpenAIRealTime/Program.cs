using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

AzureOpenAIClient aoaiClient = new(new Uri(""), new ApiKeyCredential(""));
var client = aoaiClient.GetRealtimeConversationClient("");

using RealtimeConversationSession session = await client.StartConversationSessionAsync();

// Session options control connection-wide behavior shared across all conversations,
// including audio input format and voice activity detection settings.
ConversationSessionOptions sessionOptions = new()
{
    Instructions =
        "You are a cheerful assistant that talks like a pirate. "
        + "Always inform the user when you are about to call a tool. "
        + "Prefer to call tools whenever applicable.",
    Voice = ConversationVoice.Alloy,
    Tools = { CreateSampleWeatherTool() },
    InputAudioFormat = ConversationAudioFormat.G711Alaw,
    OutputAudioFormat = ConversationAudioFormat.Pcm16,
    InputTranscriptionOptions = new() { Model = "whisper" },
};

await session.ConfigureSessionAsync(sessionOptions);

// Conversation history or text input are provided by adding messages to the conversation.
// Adding a message will not automatically begin a response turn.
await session.AddItemAsync(
    ConversationItem.CreateUserMessage(["I'm trying to decide what to wear on my trip."])
);

string inputAudioPath = FindFile("audio_weather_alaw.wav");
using Stream inputAudioStream = File.OpenRead(inputAudioPath);
_ = session.SendInputAudioAsync(inputAudioStream);

string outputAudioPath = "output.raw";
using Stream outputAudioStream = File.OpenWrite(outputAudioPath);

await foreach (ConversationUpdate update in session.ReceiveUpdatesAsync())
{
    if (update is ConversationSessionStartedUpdate sessionStartedUpdate)
    {
        Console.WriteLine($"<<< Session started. ID: {sessionStartedUpdate.SessionId}");
        Console.WriteLine();
    }

    if (update is ConversationInputSpeechStartedUpdate speechStartedUpdate)
    {
        Console.WriteLine(
            $"  -- Voice activity detection started at {speechStartedUpdate.AudioStartTime}"
        );
    }

    if (update is ConversationInputSpeechFinishedUpdate speechFinishedUpdate)
    {
        Console.WriteLine(
            $"  -- Voice activity detection ended at {speechFinishedUpdate.AudioEndTime}"
        );
    }

    // Item streaming started updates notify that the model generation process will insert a new item into
    // the conversation and begin streaming its content via content updates.
    if (update is ConversationItemStreamingStartedUpdate itemStartedUpdate)
    {
        Console.WriteLine($"  -- Begin streaming of new item");
        if (!string.IsNullOrEmpty(itemStartedUpdate.FunctionName))
        {
            Console.Write($"    {itemStartedUpdate.FunctionName}: ");
        }
    }

    // Item streaming delta updates provide a combined view into incremental item data including output
    // the audio response transcript, function arguments, and audio data.
    if (update is ConversationItemStreamingPartDeltaUpdate deltaUpdate)
    {
        Console.Write(deltaUpdate.AudioTranscript);
        Console.Write(deltaUpdate.FunctionArguments);
        outputAudioStream.Write(deltaUpdate.AudioBytes);
    }

    // Item finished updates arrive when all streamed data for an item has arrived and the
    // accumulated results are available. In the case of function calls, this is the point
    // where all arguments are expected to be present.
    if (update is ConversationItemStreamingFinishedUpdate itemFinishedUpdate)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"  -- Item streaming finished, response_id={itemFinishedUpdate.ResponseId}"
        );

        if (itemFinishedUpdate.FunctionCallId is not null)
        {
            Console.WriteLine(
                $"    + Responding to tool invoked by item: {itemFinishedUpdate.FunctionName}"
            );
            ConversationItem functionOutputItem = ConversationItem.CreateFunctionCallOutput(
                callId: itemFinishedUpdate.FunctionCallId,
                output: "70 degrees Fahrenheit and sunny"
            );
            await session.AddItemAsync(functionOutputItem);
        }
        else if (itemFinishedUpdate.MessageContentParts?.Count > 0)
        {
            Console.Write($"    + [{itemFinishedUpdate.MessageRole}]: ");
            foreach (ConversationContentPart contentPart in itemFinishedUpdate.MessageContentParts)
            {
                Console.Write(contentPart.AudioTranscript);
            }
            Console.WriteLine();
        }
    }

    if (update is ConversationInputTranscriptionFinishedUpdate transcriptionCompletedUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"  -- User audio transcript: {transcriptionCompletedUpdate.Transcript}");
        Console.WriteLine();
    }

    if (update is ConversationResponseFinishedUpdate turnFinishedUpdate)
    {
        Console.WriteLine(
            $"  -- Model turn generation finished. Status: {turnFinishedUpdate.Status}"
        );

        // Here, if we processed tool calls in the course of the model turn, we finish the
        // client turn to resume model generation. The next model turn will reflect the tool
        // responses that were already provided.
        if (turnFinishedUpdate.CreatedItems.Any(item => item.FunctionName?.Length > 0))
        {
            Console.WriteLine($"  -- Ending client turn for pending tool responses");
            await session.StartResponseAsync();
        }
        else
        {
            break;
        }
    }

    if (update is ConversationErrorUpdate errorUpdate)
    {
        Console.WriteLine();
        Console.WriteLine($"ERROR: {errorUpdate.Message}");
        break;
    }
}

Console.WriteLine(
    $"Raw output audio written to {outputAudioPath}: {outputAudioStream.Length} bytes"
);
Console.WriteLine();

static ConversationFunctionTool CreateSampleWeatherTool()
{
    return new ConversationFunctionTool()
    {
        Name = "get_weather_for_location",
        Description = "gets the weather for a location",
        Parameters = BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {
                "location": {
                  "type": "string",
                  "description": "The city and state, e.g. San Francisco, CA"
                },
                "unit": {
                  "type": "string",
                  "enum": ["c","f"]
                }
              },
              "required": ["location","unit"]
            }
            """
        ),
    };
}

static string FindFile(string fileName)
{
    for (
        string currentDirectory = Directory.GetCurrentDirectory();
        currentDirectory != null && currentDirectory != Path.GetPathRoot(currentDirectory);
        currentDirectory = Directory.GetParent(currentDirectory)?.FullName!
    )
    {
        string filePath = Path.Combine(currentDirectory, fileName);
        if (File.Exists(filePath))
        {
            return filePath;
        }
    }

    throw new FileNotFoundException($"File '{fileName}' not found.");
}
