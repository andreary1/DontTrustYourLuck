using System.Runtime.CompilerServices;

// Test-only access to MatchEngine.GrantCard (controlled card grant; not a player command).
[assembly: InternalsVisibleTo("DontTrustYourLuck.Tests.EditMode.Domain")]
[assembly: InternalsVisibleTo("DontTrustYourLuck.Tests.PlayMode")]
