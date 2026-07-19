using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    interface IGeneratesVirtualTextureFeedback
    {
        IEnumerable<string> GetFeedbackVariables();
    }
}
