using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernelFun.Models;

public class RawContentDocument
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string FileName { get; set; }
    public string Text { get; set; }
}