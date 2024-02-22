using System;

namespace m4dModels;

public class UsageLog
{
    public long Id { get; set; }
    public string UsageId { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Page { get; set; }
    public string Query { get; set; }
    public string Filter { get; set; }
}
