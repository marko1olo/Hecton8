using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    interface IHasVirtualTextureFeedback
    {
        IEnumerable<string> GetFeedbackVariables();
    }
}
