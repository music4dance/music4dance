namespace m4dModels
{
    public class MusicServiceStub(ServiceType id, char cid, string name, bool showInProfile = true) : MusicService(id, cid, name, null, null, null, null)
    {
        public override bool IsSearchable => false;

        public override bool ShowInProfile { get; } = showInProfile;
    }
}
