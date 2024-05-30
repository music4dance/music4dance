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
    public string Referrer { get; set; }
    public string UserAgent { get; set; }
}
public class UsageSummary
{
    public string UsageId { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }

}
