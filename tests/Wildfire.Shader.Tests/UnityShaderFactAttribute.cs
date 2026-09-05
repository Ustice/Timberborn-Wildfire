namespace Wildfire.Shader.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class UnityShaderFactAttribute : FactAttribute
{
    public UnityShaderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WILDFIRE_RUN_UNITY_SHADER_HARNESS") != "1")
        {
            Skip = "Requires Unity compute execution; set WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 to run.";
        }
    }
}
