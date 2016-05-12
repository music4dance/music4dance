namespace m4dModels
{
    public class MusicServiceStub : MusicService
    {
        public MusicServiceStub(ServiceType id, char cid, string name, bool showInProfile=true) :base(id,cid,name,null,null,null,null)
        {
            ShowInProfile = showInProfile;
        }

        public override bool IsSearchable => false;

        public override bool ShowInProfile { get; }
    }
}