namespace m4d.ViewModels;

public class VueModel
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Name { get; set; }
    public string Script { get; set; }
    public object Model { get; set; }
    public bool PreserveCase { get; set; }
}

public enum UseVue { No, V2, V3 }
