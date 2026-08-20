// TestSongIndex and DanceMusicTester moved to m4dModels.Sandbox (renamed SongIndexLocal and
// SandboxServiceFactory) so the no-external-service m4d.Sandbox host can reuse them too - see
// architecture/contributor-test-environments.md. These aliases keep every existing call site in
// this project compiling unchanged under the old names.

global using TestSongIndex = m4dModels.Sandbox.SongIndexLocal;
global using DanceMusicTester = m4dModels.Sandbox.SandboxServiceFactory;
