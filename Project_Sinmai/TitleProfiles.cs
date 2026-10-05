using System;

namespace Project_Sinmai;

public static class TitleProfiles
{
	public const string Sdga = "SDGA";

	public const string Sdgb = "SDGB";

	public static bool IsSdga(string? titleId)
	{
		return string.Equals(titleId, "SDGA", StringComparison.OrdinalIgnoreCase);
	}

	public static Solve.Config Create(string? titleId)
	{
		if (!IsSdga(titleId))
		{
			return Pipe.CreateConfig();
		}
		return Feed.CreateConfig();
	}
}
