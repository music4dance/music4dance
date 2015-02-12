namespace m4dModels
{
    public class MusicServiceStub : MusicService
    {
        public MusicServiceStub(ServiceType id, char cid, string name, bool showInProfile=true) :base(id,cid,name,null,null,null,null)
        {
            _showInProfile = showInProfile;
        }

        public override bool IsSearchable
        {
            get { return false; }
        }

        public override bool ShowInProfile
        {
            get { return _showInProfile; }
        }

        private readonly bool _showInProfile;
    }
}