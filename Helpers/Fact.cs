using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalRAGFun3;

public class Fact
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public required string Text { get; init; }
    public string Description { get; init; } = null;
    public string Metadata { get; init; } = null;
}

public static class Facts
{
    public static List<Fact> GetFacts()
    {
        return
        [
            new()
            {
                Text = "Andrew Herrick was born in United States",
                Description = "This is a question about Andrew Herrick's nationality.",
                Metadata = "Nationality"
            },
            new()
            {
                Text = "Andrew Herrick is a Software Engineering Manager and works for Accenture",
                Description = "This is a question about Andrew Herrick's job.",
                Metadata = "Job"
            },
            new()
            {
                Text = "Andrew Herrick's favourite hobbies are coding software and running.",
                Description = "This is a question about Andrew Herrick's hobbies.",
                Metadata = "Hobbies"
            }
        ];
    }
}