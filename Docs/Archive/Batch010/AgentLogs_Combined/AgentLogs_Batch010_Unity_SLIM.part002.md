at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
*** Tundra build failed (191.84 seconds - 0:03:11), 1888 items updated, 3439 evaluated
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp2"
Output:
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp2"
Output:
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Script Compilation Error for: ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.MockDomain.Runtime.dll (+pdb)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\Compilation\Unity.ILPP.Trigger\Unity.ILPP.Trigger.exe" @"Library\Bee\artifacts\rsp\12719471298722492838.rsp"
Output:
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
Assets\MapMagic\Tools\Extensions\Texture2DExtensions.cs(350,48): warning CS0618: 'TextureFormat.PVRTC_RGBA4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\Feel\NiceVibrations\Define\NiceVibrationsDefineSymbols.cs(65,45): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\Feel\NiceVibrations\Define\NiceVibrationsDefineSymbols.cs(68,13): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\_Project\Scripts\Input\InputManager.cs(67,22): warning CS0414: field 'InputManager._playerActionsSubscribed' is assigned but its value is never used
Assets\_Project\Scripts\Input\InputManager.cs(68,22): warning CS0414: field 'InputManager._uiActionsSubscribed' is assigned but its value is never used
Assets\AstarPathfindingProject\Core\AI\AIBase.cs(434,130): warning CS0618: 'Rigidbody2D.isKinematic' is obsolete: 'isKinematic has been deprecated. Please use bodyType.'
Assets\AstarPathfindingProject\Generators\Utilities\RecastMeshGatherer.cs(47,93): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\AstarPathfindingProject\Generators\Utilities\RecastMeshGatherer.cs(47,23): warning CS0618: 'Object.FindObjectsByType (FindObjectsInactive, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\AstarPathfindingProject\Core\Serialization\TinyJson.cs(344,116): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\AstarPathfindingProject\Core\Serialization\TinyJson.cs(344,28): warning CS0618: 'Object.FindObjectsByType (FindObjectsInactive, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\MeshBaker\scripts\MB3_MeshBakerRoot.cs(163,47): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\scripts\MB3_MeshBakerRoot.cs(165,36): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\scripts\MB3_MBVersionConcrete.cs(80,60): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\MeshBaker\scripts\MB3_MBVersionConcrete.cs(80,20): warning CS0618: 'Object.FindObjectsByType(Type, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType(Type) or FindObjectsByType(Type, FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(37,16): warning CS0618: 'ShaderUtil.GetPropertyCount(Shader)' is obsolete: 'Use Shader.GetPropertyCount instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(40,14): warning CS0618: 'ShaderUtil.GetPropertyType(Shader, int)' is obsolete: 'Use Shader.GetPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(41,16): warning CS0618: 'ShaderUtil.GetPropertyName(Shader, int)' is obsolete: 'Use Shader.GetPropertyName instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(44,11): warning CS0618: 'ShaderUtil.ShaderPropertyType' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(47,11): warning CS0618: 'ShaderUtil.ShaderPropertyType' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(50,11): warning CS0618: 'ShaderUtil.ShaderPropertyType' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(53,11): warning CS0618: 'ShaderUtil.ShaderPropertyType' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(56,11): warning CS0618: 'ShaderUtil.ShaderPropertyType' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType instead.'
Assets\AmplifyImpostors\Plugins\Scripts\HelperExtensions.cs(56,11): warning CS0618: 'ShaderUtil.ShaderPropertyType.TexEnv' is obsolete: 'Use UnityEngine.Rendering.ShaderPropertyType.Texture instead.'
Packages\com.waveharmonic.crest\Runtime\Scripts\Utility\Shared\Component\CustomBehaviour.cs(74,101): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Packages\com.waveharmonic.crest\Runtime\Scripts\Utility\Shared\Component\CustomBehaviour.cs(74,37): warning CS0618: 'Object.FindObjectsByType (FindObjectsInactive, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Packages\com.waveharmonic.crest\Runtime\Scripts\Utility\Shared\Component\ManagedBehaviour.cs(64,24): warning CS0618: 'Object.FindFirstObjectByType ()' is obsolete: 'FindFirstObjectByType has been deprecated because it relies on instance ID ordering. Use FindAnyObjectByType instead, which does not depend on ordering.'
Packages\com.waveharmonic.crest\Runtime\Scripts\Utility\Shared\Component\EditorBehaviour.cs(91,50): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Packages\com.waveharmonic.crest\Runtime\Scripts\Utility\Shared\Component\EditorBehaviour.cs(91,21): warning CS0618: 'Object.FindObjectsByType(Type, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType(Type) or FindObjectsByType(Type, FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\GPUInstancer\Scripts\Core\Contract\GPUInstancerTerrainManager.cs(270,146): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\GPUInstancer\Scripts\GPUInstancerEditorSimulator.cs(69,17): warning CS0618: 'RenderPipelineManager.beginFrameRendering' is obsolete: 'beginFrameRendering is deprecated. Use beginContextRendering instead. #from 2023.3'
Assets\GPUInstancer\Scripts\GPUInstancerEditorSimulator.cs(90,17): warning CS0618: 'RenderPipelineManager.beginFrameRendering' is obsolete: 'beginFrameRendering is deprecated. Use beginContextRendering instead. #from 2023.3'
Assets\GPUInstancer\Scripts\GPUInstancerEditorSimulator.cs(147,21): warning CS0618: 'RenderPipelineManager.beginFrameRendering' is obsolete: 'beginFrameRendering is deprecated. Use beginContextRendering instead. #from 2023.3'
Assets\GPUInstancer\Scripts\GPUInstancerEditorSimulator.cs(148,21): warning CS0618: 'RenderPipelineManager.beginFrameRendering' is obsolete: 'beginFrameRendering is deprecated. Use beginContextRendering instead. #from 2023.3'
Assets\GPUInstancer\Scripts\GPUInstancerDetailManager.cs(520,106): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\GPUInstancer\Scripts\GPUInstancerDetailManager.cs(556,106): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\GPUInstancer\Scripts\Core\Static\GPUInstancerUtility.cs(885,158): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\GPUInstancer\Scripts\Core\Static\GPUInstancerUtility.cs(885,262): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\GPUInstancer\Scripts\Core\Static\GPUInstancerUtility.cs(1351,50): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Feel\MMTools\Tools\MMPhysics\MMRigidbodyInterface.cs(121,13): warning CS0618: 'Rigidbody2D.isKinematic' is obsolete: 'isKinematic has been deprecated. Please use bodyType.'
Assets\Feel\MMTools\Tools\MMPhysics\MMRigidbodyInterface.cs(253,5): warning CS0618: 'Rigidbody2D.isKinematic' is obsolete: 'isKinematic has been deprecated. Please use bodyType.'
Assets\Feel\MMTools\Tools\MMHelpers\MMDebug.cs(563,41): warning CS0618: 'Object.FindObjectOfType(Type)' is obsolete: 'Object.FindObjectOfType has been deprecated. Use Object.FindFirstObjectByType instead or if finding any instance is acceptable faster Object.FindAnyObjectByType'
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Assets\Crest\Crest\Scripts\Helpers\Helpers.cs(368,48): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\Crest\Crest\Scripts\Helpers\Helpers.cs(368,20): warning CS0618: 'Object.FindObjectsByType (FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Packages\com.unity.shadergraph\Editor\Drawing\Views\MaterialGraphView.cs(66,18): warning CS0618: 'ITransform.position' is obsolete: 'When reading value, use VisualElement.resolvedStyle.translate. When writing value, use VisualElement.style.translate instead.'
Packages\com.unity.shadergraph\Editor\Drawing\Views\MaterialGraphView.cs(68,31): warning CS0618: 'ITransform.position' is obsolete: 'When reading value, use VisualElement.resolvedStyle.translate. When writing value, use VisualElement.style.translate instead.'
Packages\com.unity.shadergraph\Editor\Drawing\Views\MaterialGraphView.cs(69,28): warning CS0618: 'ITransform.scale' is obsolete: 'When reading value, use VisualElement.resolvedStyle.scale. When writing value, use VisualElement.style.scale instead.'
Assets\MapMagic\Brush\Editor\BrushGraphTemplate.cs(31,37): warning CS0618: 'EndNameEditAction' is obsolete: 'EndNameEditAction is obsolete. Use AssetCreationEndAction that uses EntityId instead of int for instance IDs.'
Assets\MapMagic\Nodes\Editor\GraphTemplates.cs(31,32): warning CS0618: 'EndNameEditAction' is obsolete: 'EndNameEditAction is obsolete. Use AssetCreationEndAction that uses EntityId instead of int for instance IDs.'
Assets\MapMagic\Brush\Editor\BrushGraphTemplate.cs(24,5): warning CS0618: 'ProjectWindowUtil.StartNameEditingIfProjectWindowExists(int, EndNameEditAction, string, Texture2D, string)' is obsolete: 'StartNameEditingIfProjectWindowExists(int, EndNameEditAction, string, Texture2D, string) is obsolete. Use StartNameEditingIfProjectWindowExists(EntityId, AssetCreationEndAction, string, Texture2D, string) instead.'
Assets\MapMagic\Nodes\Editor\GraphTemplates.cs(24,5): warning CS0618: 'ProjectWindowUtil.StartNameEditingIfProjectWindowExists(int, EndNameEditAction, string, Texture2D, string)' is obsolete: 'StartNameEditingIfProjectWindowExists(int, EndNameEditAction, string, Texture2D, string) is obsolete. Use StartNameEditingIfProjectWindowExists(EntityId, AssetCreationEndAction, string, Texture2D, string) instead.'
Assets\MapMagic\Core\Editor\MapMagicInspector.cs(686,27): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Editor\MapMagicInspector.cs(699,27): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Editor\MapMagicInspector.cs(711,4): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\MapMagic\Nodes\Editor\GraphWindow.cs(1153,30): warning CS0618: 'EditorUtility.InstanceIDToObject(int)' is obsolete: 'InstanceIDToObject(int) is obsolete. Use EditorUtility.EntityIdToObject instead.'
Assets\MapMagic\Brush\Editor\BrushInspector.cs(262,99): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\MapMagic\Brush\Editor\BrushInspector.cs(262,32): warning CS0618: 'Object.FindObjectsByType (FindObjectsInactive, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\GPUInstancer\Scripts\Editor\GPUInstancerDefines.cs(41,56): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\GPUInstancer\Scripts\Editor\GPUInstancerDefines.cs(46,17): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\GPUInstancer\Scripts\Editor\PackageImporter\GPUIPackageImporter.cs(48,34): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\AstarPathfindingProject\Editor\AIBaseEditor.cs(116,131): warning CS0618: 'Rigidbody2D.isKinematic' is obsolete: 'isKinematic has been deprecated. Please use bodyType.'
Assets\AstarPathfindingProject\Editor\AstarUpdateChecker.cs(213,63): warning CS0618: 'Object.FindObjectOfType(Type)' is obsolete: 'Object.FindObjectOfType has been deprecated. Use Object.FindFirstObjectByType instead or if finding any instance is acceptable faster Object.FindAnyObjectByType'
Assets\AstarPathfindingProject\Editor\AstarUpdateChecker.cs(215,19): warning CS0618: 'Object.FindObjectOfType(Type)' is obsolete: 'Object.FindObjectOfType has been deprecated. Use Object.FindFirstObjectByType instead or if finding any instance is acceptable faster Object.FindAnyObjectByType'
Assets\AstarPathfindingProject\Editor\GraphEditors\GridGeneratorEditor.cs(128,69): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use FindObjectsByType overloads that do not take FindObjectsSortMode parameter.'
Assets\AstarPathfindingProject\Editor\GraphEditors\GridGeneratorEditor.cs(128,20): warning CS0618: 'Object.FindObjectsByType (FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType () or FindObjectsByType (FindObjectsInactive) instead. InstanceID will be replaced in future with EntityId and previous sort order cannot be maintained.'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(211,46): warning CS0618: 'TextureImporterFormat.PVRTC_RGB2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(212,46): warning CS0618: 'TextureImporterFormat.PVRTC_RGB4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(213,46): warning CS0618: 'TextureImporterFormat.PVRTC_RGBA2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(214,46): warning CS0618: 'TextureImporterFormat.PVRTC_RGBA4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB_TextureBakerEditorConfigureMultiMaterials.cs(321,62): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB_TextureBakerEditorConfigureMultiMaterials.cs(328,53): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(454,22): warning CS0618: 'TextureFormat.PVRTC_RGB2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(455,41): warning CS0618: 'TextureImporterFormat.PVRTC_RGB2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(457,22): warning CS0618: 'TextureFormat.PVRTC_RGB4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(458,41): warning CS0618: 'TextureImporterFormat.PVRTC_RGB4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(460,22): warning CS0618: 'TextureFormat.PVRTC_RGBA2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(461,41): warning CS0618: 'TextureImporterFormat.PVRTC_RGBA2' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(463,22): warning CS0618: 'TextureFormat.PVRTC_RGBA4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MBVersionConcreteEditor.cs(464,41): warning CS0618: 'TextureImporterFormat.PVRTC_RGBA4' is obsolete: 'Texture compression format PVRTC has been deprecated and will be removed in future release'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorWindowAddObjectsTab.cs(487,71): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorWindowAddObjectsTab.cs(490,62): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorWindowAnalyseSceneTab.cs(475,62): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorWindowAnalyseSceneTab.cs(482,53): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorInternal.cs(290,37): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\MeshBaker\Editor\MB3_MeshBakerEditorInternal.cs(389,37): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Editor\x64\Bakery\scripts\ftDefine.cs(27,23): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\Editor\x64\Bakery\scripts\ftDefine.cs(32,13): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\Editor\x64\Bakery\scripts\ftLightMeshInspector.cs(76,48): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Editor\x64\Bakery\scripts\ftSkyLightInspector.cs(59,48): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Editor\x64\Bakery\scripts\ftPointLightInspector.cs(383,48): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Feel\MMTools\Editor\MMAttributes\MMMonoBehaviourDrawer.cs(72,124): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\Feel\MMTools\Editor\MMAttributes\MMMonoBehaviourDrawer.cs(133,102): warning CS0618: 'Object.GetInstanceID()' is obsolete: 'GetInstanceID is deprecated. Use GetEntityId instead. This will be removed in future version.'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Packages\com.waveharmonic.crest\Shared\Scripts\AlignSceneViewToCamera.cs(37,23): warning CS0618: 'SceneHandle.implicit operator int(SceneHandle)' is obsolete: 'Implicit conversion from SceneHandle to int is deprecated. Use SceneHandle.GetRawData() instead'
Packages\com.waveharmonic.crest\Shared\Scripts\AlignSceneViewToCamera.cs(45,17): warning CS0618: 'SceneHandle.implicit operator SceneHandle(int)' is obsolete: 'Implicit conversion from int to SceneHandle is deprecated. Use SceneHandle.FromRawData(ulong) instead'
Packages\com.waveharmonic.crest\Shared\Scripts\AlignSceneViewToCamera.cs(47,23): warning CS0618: 'SceneHandle.implicit operator int(SceneHandle)' is obsolete: 'Implicit conversion from SceneHandle to int is deprecated. Use SceneHandle.GetRawData() instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(62,21): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(78,5): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(110,23): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(189,21): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(215,21): warning CS0618: 'PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup)' is obsolete: 'Use GetScriptingDefineSymbols(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(233,4): warning CS0618: 'PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup, string)' is obsolete: 'Use SetScriptingDefineSymbols(NamedBuildTarget buildTarget, string defines) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(308,8): warning CS0618: 'PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup)' is obsolete: 'Use GetApiCompatibilityLevel(NamedBuildTarget buildTarget) instead'
Assets\MapMagic\Core\Plugins\SettingsWindow.cs(314,7): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
AssetDatabase: script compilation time: 221.640187s
[ScriptCompilation] Requested script compilation because: AssetDatabase observed changes in script compilation related files
[Licensing::Client] Successfully resolved entitlement details
Starting: C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\bee_backend.exe --ipc --defer-dag-verification --dagfile="Library/Bee/1900b0aEDbg.dag" --continue-on-failure --profile="Library/Bee/backend1.traceevents" ScriptAssemblies
WorkingDir: C:/hades/Hecton8
Total cache size 320475283
Total cache size after purge 267290415, took 00:00:03.3842095
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp2"
Output:
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp2"
Output:
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Script Compilation Error for: ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.MockDomain.Runtime.dll (+pdb)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\Compilation\Unity.ILPP.Trigger\Unity.ILPP.Trigger.exe" @"Library\Bee\artifacts\rsp\12719471298722492838.rsp"
Output:
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
ExitCode: 3 Duration: 8s310ms
[2420/3439 3s] ILPP-Configuration Library/ilpp-configuration.nevergeneratedoutput
[3120/3439 2s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Narrative.Prologue.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs"
-langversion:9.0
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
[3121/3439 2s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.World.ProceduralWreckage.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageContracts.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
[3122/3439 3s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.World.ProceduralCoral.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralContracts.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralGpuUploadDispatcher.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
[3123/3439 3s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Core.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:UNITY_ADDRESSABLES_EXIST
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/GPUInstancer.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Ecology.Migration.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Animation.IK.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Echolocation.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Virtualization.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Virtualization.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Bootstrap.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Cartography.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Bucketing.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Database.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Persistence.Paging.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Scheduling.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Environment.Fluids.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Environment.Fluids.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Input.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Algorithms.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Corrosion.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Corrosion.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.Grid.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.Grid.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.CCD.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.Determinism.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.Tethers.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.UI.Diegetic.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Vehicles.Physics.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.Terrain.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Addressables.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.InputSystem.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Profiling.Core.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.RenderPipelines.Core.Runtime.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.RenderPipelines.Universal.Runtime.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.ResourceManager.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.TextMeshPro.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs"
"Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs"
"Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs"
"Assets/_Project/Scripts/AI/Perception/RetinalAdaptationVault.cs"
"Assets/_Project/Scripts/AI/Perception/RetinalExposureMath.cs"
"Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs"
"Assets/_Project/Scripts/AcousticZoneController.cs"
"Assets/_Project/Scripts/AmbientWaterMotion.cs"
"Assets/_Project/Scripts/AmbientWaterMotionManager.cs"
"Assets/_Project/Scripts/AmbientWaterMotionProfile.cs"
"Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs"
"Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs"
"Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs"
"Assets/_Project/Scripts/AssemblyInfo.cs"
"Assets/_Project/Scripts/AsyncLoadHelper.cs"
"Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"
"Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs"
"Assets/_Project/Scripts/Atmosphere/AtmosphericLightingState.cs"
"Assets/_Project/Scripts/Atmosphere/BaseAtmosphereEngine.cs"
"Assets/_Project/Scripts/Atmosphere/BaseAtmosphereMath.cs"
"Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs"
"Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuAtmosphereWaveTunerWindow.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherMath.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherProfile.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherVfxRig.cs"
"Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs"
"Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs"
"Assets/_Project/Scripts/AtmosphereProfile.cs"
"Assets/_Project/Scripts/Audio/AcousticReverbPresetTrigger.cs"
"Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs"
"Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs"
"Assets/_Project/Scripts/Audio/AudioMaterialProfile.cs"
"Assets/_Project/Scripts/Audio/DeepPsychosisController.cs"
"Assets/_Project/Scripts/Audio/Editor/AbyssalAcousticsTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/AdaptiveAudioTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/AudioImportDictator.cs"
"Assets/_Project/Scripts/Audio/Editor/AudioOmegaAutonomySmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/DSPThreadSafetySmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/GranularSynthTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/SabineReverbDspTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs"
"Assets/_Project/Scripts/Audio/HectonMusicBiomeProfile.cs"
"Assets/_Project/Scripts/Audio/HectonMusicClip.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirector.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirectorAnchor.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs"
"Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs"
"Assets/_Project/Scripts/Audio/MusicVoicePool.cs"
"Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalMetallicGrainBank.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs"
"Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs"
"Assets/_Project/Scripts/Audio/VocalWarningSystem.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogData.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogDiscoveryBitMask.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs"
"Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs"
"Assets/_Project/Scripts/AutomationSmokeTester.cs"
"Assets/_Project/Scripts/BarterRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BaseModule.cs"
"Assets/_Project/Scripts/BaseModuleTemplate.cs"
"Assets/_Project/Scripts/BaseStressRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BeaconDeployerTool.cs"
"Assets/_Project/Scripts/BeaconNetworkSystem.cs"
"Assets/_Project/Scripts/BeaconRuntime.cs"
"Assets/_Project/Scripts/BiomeDiscoveryBitMask.cs"
"Assets/_Project/Scripts/BiomeMatrixDirector.cs"
"Assets/_Project/Scripts/BiomeSamplerCache.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapController.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapHealthMonitor.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapRegistryCycleValidator.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapRouteEnforcer.cs"
"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"
"Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs"
"Assets/_Project/Scripts/Bootstrap/SceneGuard.cs"
"Assets/_Project/Scripts/Bootstrap/SceneInstantiationGate.cs"
"Assets/_Project/Scripts/Build/BuildInfo.cs"
"Assets/_Project/Scripts/Build/BuildInfoHudPresenter.cs"
"Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs"
"Assets/_Project/Scripts/BuildableData.cs"
"Assets/_Project/Scripts/BuilderRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BuilderTool.cs"
"Assets/_Project/Scripts/BuoyancyObject.cs"
"Assets/_Project/Scripts/BuoyancyProfile.cs"
"Assets/_Project/Scripts/CameraJuiceProcessor.cs"
"Assets/_Project/Scripts/CaveBioRootsGenerator.cs"
"Assets/_Project/Scripts/CaveBiomeTemplate.cs"
"Assets/_Project/Scripts/CaveDressingConfig.cs"
"Assets/_Project/Scripts/CaveFaunaContext.cs"
"Assets/_Project/Scripts/CaveGlowingTissueRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveGraphGenerator.cs"
"Assets/_Project/Scripts/CaveRuntimeBoundsUtility.cs"
"Assets/_Project/Scripts/CaveSedimentShelfRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveServiceRemnantRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveTypes.cs"
"Assets/_Project/Scripts/CaveWallGrowthRuntimeBuilder.cs"
"Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs"
"Assets/_Project/Scripts/Compatibility/LegacyStubs/DefaultFlowFieldProfile.cs"
"Assets/_Project/Scripts/ComponentCache.cs"
"Assets/_Project/Scripts/Construction/AutomataTemplate.cs"
"Assets/_Project/Scripts/Construction/AutonomousExtractorJobs.cs"
"Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs"
"Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs"
"Assets/_Project/Scripts/Construction/BaseLogisticsNetwork.cs"
"Assets/_Project/Scripts/Construction/BaseModuleNavModifier.cs"
"Assets/_Project/Scripts/Construction/BatteryBankModule.cs"
"Assets/_Project/Scripts/Construction/BatteryChargerModule.cs"
"Assets/_Project/Scripts/Construction/BotanyPlanterModule.cs"
"Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs"
"Assets/_Project/Scripts/Construction/ConstructionSignals.cs"
"Assets/_Project/Scripts/Construction/CultivationManager.cs"
"Assets/_Project/Scripts/Construction/DeepDrillModule.cs"
"Assets/_Project/Scripts/Construction/DroneCognitionJob.cs"
"Assets/_Project/Scripts/Construction/DroneFleetManager.cs"
"Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs"
"Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs"
"Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs"
"Assets/_Project/Scripts/Construction/HabitatGraphManager.cs"
"Assets/_Project/Scripts/Construction/HabitatStressJobs.cs"
"Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeRoutingKernel.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs"
"Assets/_Project/Scripts/Construction/LogisticsRouteScratchMemory.cs"
"Assets/_Project/Scripts/Construction/LogisticsSorterModule.cs"
"Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs"
"Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs"
"Assets/_Project/Scripts/Construction/ModuleIntegrityComponent.cs"
"Assets/_Project/Scripts/Construction/ModuleLifeSupportComponent.cs"
"Assets/_Project/Scripts/Construction/RepairDroneEntity.cs"
"Assets/_Project/Scripts/Construction/RepairDroneHub.cs"
"Assets/_Project/Scripts/Construction/RepairStation.cs"
"Assets/_Project/Scripts/Construction/StructuralIntegrityProfile.cs"
"Assets/_Project/Scripts/Construction/TransitionHatchMeshState.cs"
"Assets/_Project/Scripts/Construction/VRConstructionWeldTarget.cs"
"Assets/_Project/Scripts/Construction/VRPipeBlueprintPreview.cs"
"Assets/_Project/Scripts/Construction/VehicleDockingModule.cs"
"Assets/_Project/Scripts/Construction/WaterPumpModule.cs"
"Assets/_Project/Scripts/ConstructionManager.cs"
"Assets/_Project/Scripts/ControlScheme.cs"
"Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs"
"Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs"
"Assets/_Project/Scripts/Core/Bridge/Generated/H8DesignFacadeContracts.generated.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeBinaryLayoutVerifier.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs"
"Assets/_Project/Scripts/Core/Bridge/H8DesignDataFacade.cs"
"Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs"
"Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs"
"Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs"
"Assets/_Project/Scripts/Core/BurstCallback.cs"
"Assets/_Project/Scripts/Core/CameraJuiceSignals.cs"
"Assets/_Project/Scripts/Core/CinematicMath.cs"
"Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs"
"Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs"
"Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs"
"Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs"
"Assets/_Project/Scripts/Core/Content/ContentSaveSlotTopology.cs"
"Assets/_Project/Scripts/Core/Content/ObjectBatchBase.cs"
"Assets/_Project/Scripts/Core/Content/VisibilityProxyBase.cs"
"Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs"
"Assets/_Project/Scripts/Core/Data/H8DataBaker.cs"
"Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs"
"Assets/_Project/Scripts/Core/Data/H8StaticDataSanity.cs"
"Assets/_Project/Scripts/Core/Data/InventoryCost.cs"
"Assets/_Project/Scripts/Core/Data/StaticDataStore.cs"
"Assets/_Project/Scripts/Core/DependencyAttribute.cs"
"Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs"
"Assets/_Project/Scripts/Core/DeterministicReplaySeed.cs"
"Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyePdaCommandConsole.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/Editor/ArchitectEyeBlackBoxTimelineViewer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultProbeUtility.cs"
"Assets/_Project/Scripts/Core/DispatcherJobFence.cs"
"Assets/_Project/Scripts/Core/DistanceMath.cs"
"Assets/_Project/Scripts/Core/DodReplayRecorder.cs"
"Assets/_Project/Scripts/Core/Editor/InputCurveHapticsTunerWindow.cs"
"Assets/_Project/Scripts/Core/EnumFastComparer.cs"
"Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs"
"Assets/_Project/Scripts/Core/FixedCharBuffer.cs"
"Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs"
"Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs"
"Assets/_Project/Scripts/Core/GCMonitor.cs"
"Assets/_Project/Scripts/Core/GameStartContext.cs"
"Assets/_Project/Scripts/Core/Generated/H8Hashes.cs"
"Assets/_Project/Scripts/Core/Generated/H8LoreHashes.cs"
"Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs"
"Assets/_Project/Scripts/Core/GlobalRegistry.cs"
"Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"
"Assets/_Project/Scripts/Core/GlobalSignals.cs"
"Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs"
"Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs"
"Assets/_Project/Scripts/Core/H8Debug.cs"
"Assets/_Project/Scripts/Core/HardwareProfileCatalog.cs"
"Assets/_Project/Scripts/Core/HardwareTierDetector.cs"
"Assets/_Project/Scripts/Core/HectonArenaAllocator.cs"
"Assets/_Project/Scripts/Core/HectonLayerMasks.cs"
"Assets/_Project/Scripts/Core/HectonNativeBridge.cs"
"Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs"
"Assets/_Project/Scripts/Core/HectonShadowBudgetLight.cs"
"Assets/_Project/Scripts/Core/HectonSpatialIntrinsics.cs"
"Assets/_Project/Scripts/Core/HectonThreadPriorityPolicy.cs"
"Assets/_Project/Scripts/Core/HectonUrpShadowBudgetGuard.cs"
"Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs"
"Assets/_Project/Scripts/Core/HectonXRManager.cs"
"Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs"
"Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs"
"Assets/_Project/Scripts/Core/HomeostasisBrain.cs"
"Assets/_Project/Scripts/Core/IDispatcherRaycastReceiver.cs"
"Assets/_Project/Scripts/Core/IOceanVisualBridge.cs"
"Assets/_Project/Scripts/Core/IPlatformIntegration.cs"
"Assets/_Project/Scripts/Core/InputDeterminismDtos.cs"
"Assets/_Project/Scripts/Core/InputDispatcher.cs"
"Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs"
"Assets/_Project/Scripts/Core/JobAdmissionTelemetryBridge.cs"
"Assets/_Project/Scripts/Core/JobFenceManager.cs"
"Assets/_Project/Scripts/Core/LogisticsPipeBuilder.cs"
"Assets/_Project/Scripts/Core/MacroDatabaseSignalBridge.cs"
"Assets/_Project/Scripts/Core/MaterialPropertyBlockRegistry.cs"
"Assets/_Project/Scripts/Core/MathGuard.cs"
"Assets/_Project/Scripts/Core/MemoryBudgetTracker.cs"
"Assets/_Project/Scripts/Core/MemoryInquisitor.cs"
"Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs"
"Assets/_Project/Scripts/Core/NativeAllocationTrackerRuntimeBridge.cs"
"Assets/_Project/Scripts/Core/NativeArenaAllocator.cs"
"Assets/_Project/Scripts/Core/NativeArenaArray.cs"
"Assets/_Project/Scripts/Core/NativeBitmask256.cs"
"Assets/_Project/Scripts/Core/NativeMemorySentinel.cs"
"Assets/_Project/Scripts/Core/NativeMemoryTrackingBridgeInstaller.cs"
"Assets/_Project/Scripts/Core/NativeQuery.cs"
"Assets/_Project/Scripts/Core/NativeRingBuffer.cs"
"Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs"
"Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs"
"Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs"
"Assets/_Project/Scripts/Core/PlatformAdaptiveBudgetGovernor.cs"
"Assets/_Project/Scripts/Core/PlatformBatteryWatchdog.cs"
"Assets/_Project/Scripts/Core/PlatformPrecisionClock.cs"
"Assets/_Project/Scripts/Core/PlayerInputState.cs"
"Assets/_Project/Scripts/Core/PlayerInventoryManager.cs"
"Assets/_Project/Scripts/Core/PlayerLookTargetPromptCache.cs"
"Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs"
"Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs"
"Assets/_Project/Scripts/Core/PlayerSensoryManager.cs"
"Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs"
"Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs"
"Assets/_Project/Scripts/Core/RebindingManager.cs"
"Assets/_Project/Scripts/Core/RegistryBucket.cs"
"Assets/_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs"
"Assets/_Project/Scripts/Core/RuntimeWatchdog.cs"
"Assets/_Project/Scripts/Core/SceneRuntimeService.cs"
"Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs"
"Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs"
"Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs"
"Assets/_Project/Scripts/Core/Signals/SignalCorridorMockSignalGenerators.cs"
"Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"
"Assets/_Project/Scripts/Core/StackQueue.cs"
"Assets/_Project/Scripts/Core/SteamDeckInputPal.cs"
"Assets/_Project/Scripts/Core/SteamDeckRadialMenu.cs"
"Assets/_Project/Scripts/Core/SystemDispatcher.cs"
"Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs"
"Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs"
"Assets/_Project/Scripts/Core/UIStateStore.cs"
"Assets/_Project/Scripts/Core/UnsafeArenaAllocator.cs"
"Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs"
"Assets/_Project/Scripts/Core/VRAMBudgetTracker.cs"
"Assets/_Project/Scripts/Core/VoxelUnsafeExtensions.cs"
"Assets/_Project/Scripts/Core/ZeroGCFormatter.cs"
"Assets/_Project/Scripts/CraftingEvents.cs"
"Assets/_Project/Scripts/CraftingRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/CraftingSystem.cs"
"Assets/_Project/Scripts/CrashTelemetryBuffer.cs"
"Assets/_Project/Scripts/CreatureArchetypeData.cs"
"Assets/_Project/Scripts/CurrentManager.cs"
"Assets/_Project/Scripts/CurrentVolume.cs"
"Assets/_Project/Scripts/Data/BiomeContentPackContract.cs"
"Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs"
"Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs"
"Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs"
"Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs"
"Assets/_Project/Scripts/Data/ResearchDataTemplate.cs"
"Assets/_Project/Scripts/Data/ScannerUpgradeTemplate.cs"
"Assets/_Project/Scripts/Data/XenoBiologyTree.cs"
"Assets/_Project/Scripts/DemoDoor.cs"
"Assets/_Project/Scripts/DemoFirstPersonController.cs"
"Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs"
"Assets/_Project/Scripts/Dev/BotController.cs"
"Assets/_Project/Scripts/Dev/CelestialCataclysmSmokeTester.cs"
"Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs"
"Assets/_Project/Scripts/Dev/EditorPlayModeDiagnostics.cs"
"Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs"
"Assets/_Project/Scripts/Dev/IL2CPPCrashTelemetryDebugMenu.cs"
"Assets/_Project/Scripts/Dev/NarrativeProgressionSmokeTester.cs"
"Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs"
"Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Economy/EconomyInflationProfile.cs"
"Assets/_Project/Scripts/Economy/EconomyRuntimeInstaller.cs"
"Assets/_Project/Scripts/Economy/LootTable.cs"
"Assets/_Project/Scripts/Economy/RecyclingRegistry.cs"
"Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs"
"Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs"
"Assets/_Project/Scripts/Economy/ResourceStack.cs"
"Assets/_Project/Scripts/Economy/ScrapManager.cs"
"Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs"
"Assets/_Project/Scripts/Ecosystem/CreatureGeneticsProfile.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemMigrationProfile.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs"
"Assets/_Project/Scripts/Ecosystem/Editor/MacroEcosystemTunerWindow.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaBiomeMutationDefinition.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaBrain.Ecosystem.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGeneticTraits.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs"
"Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs"
"Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs"
"Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs"
"Assets/_Project/Scripts/EncounterDirector.cs"
"Assets/_Project/Scripts/EncounterProfile.cs"
"Assets/_Project/Scripts/EntityChangeDetector.cs"
"Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs"
"Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs"
"Assets/_Project/Scripts/Environment/WeatherEvents.cs"
"Assets/_Project/Scripts/Environment/WeatherProfile.cs"
"Assets/_Project/Scripts/EnvironmentState.cs"
"Assets/_Project/Scripts/EnvironmentalAnalyzerTool.cs"
"Assets/_Project/Scripts/FabricationAssemblerRuntime.cs"
"Assets/_Project/Scripts/FabricationRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Fabricator.cs"
"Assets/_Project/Scripts/FabricatorPhysicalActuator.cs"
"Assets/_Project/Scripts/FastCandidateMap.cs"
"Assets/_Project/Scripts/Fauna/ApexTerritoryProfile.cs"
"Assets/_Project/Scripts/Fauna/CreatureDamageManager.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.Foveated.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.cs"
"Assets/_Project/Scripts/Fauna/FaunaDataTemplate.cs"
"Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs"
"Assets/_Project/Scripts/Fauna/FaunaLogicalLodTier.cs"
"Assets/_Project/Scripts/Fauna/FaunaPOI.cs"
"Assets/_Project/Scripts/Fauna/FaunaPresentationService.cs"
"Assets/_Project/Scripts/Fauna/FaunaScanRuntimeRegistry.cs"
"Assets/_Project/Scripts/Fauna/FaunaSensorSuite.cs"
"Assets/_Project/Scripts/Fauna/FaunaSimplifiedRagdollHandoff.cs"
"Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs"
"Assets/_Project/Scripts/Fauna/FaunaSpeciesProfile.cs"
"Assets/_Project/Scripts/Fauna/FaunaStateMachine.cs"
"Assets/_Project/Scripts/Fauna/FaunaSteeringEngine.cs"
"Assets/_Project/Scripts/Fauna/FaunaTentacleConstrainedIk.cs"
"Assets/_Project/Scripts/Fauna/FaunaTier1LodProxyRegistry.cs"
"Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs"
"Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs"
"Assets/_Project/Scripts/Fauna/MesofaunaFsmDebugGizmo.cs"
"Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs"
"Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs"
"Assets/_Project/Scripts/FaunaBiomeData.cs"
"Assets/_Project/Scripts/FaunaDirector.cs"
"Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/FieldLoadoutAdvisor.cs"
"Assets/_Project/Scripts/FieldOperationLogSystem.cs"
"Assets/_Project/Scripts/FieldTargetDescriptor.cs"
"Assets/_Project/Scripts/FieldTargetSemantics.cs"
"Assets/_Project/Scripts/FieldToolRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/FlashlightTool.cs"
"Assets/_Project/Scripts/FlowFieldProfile.cs"
"Assets/_Project/Scripts/FlowFieldVisualizer.cs"
"Assets/_Project/Scripts/FluidCompartmentTemplate.cs"
"Assets/_Project/Scripts/FluidIncursionSmokeTester.cs"
"Assets/_Project/Scripts/GameTickManager.cs"
"Assets/_Project/Scripts/Gameplay/BarterOfferCatalog.cs"
"Assets/_Project/Scripts/Gameplay/BarterOfferData.cs"
"Assets/_Project/Scripts/Gameplay/BaseAirlock.cs"
"Assets/_Project/Scripts/Gameplay/BaseAirlockEvents.cs"
"Assets/_Project/Scripts/Gameplay/BaseModuleCondensationSurface.cs"
"Assets/_Project/Scripts/Gameplay/BatteryCharger.cs"
"Assets/_Project/Scripts/Gameplay/BeaconRegistry.cs"
"Assets/_Project/Scripts/Gameplay/BioReactor.cs"
"Assets/_Project/Scripts/Gameplay/CelestialCataclysmSystem.cs"
"Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs"
"Assets/_Project/Scripts/Gameplay/Combat/BallisticsEditorFacade.cs"
"Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs"
"Assets/_Project/Scripts/Gameplay/ConsumableItem.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkMath.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs"
"Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs"
"Assets/_Project/Scripts/Gameplay/DebrisManager.cs"
"Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs"
"Assets/_Project/Scripts/Gameplay/DeployableFlare.cs"
"Assets/_Project/Scripts/Gameplay/DirectorMissionBridge.cs"
"Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs"
"Assets/_Project/Scripts/Gameplay/EndingSystem.cs"
"Assets/_Project/Scripts/Gameplay/EndingTerminalInteractable.cs"
"Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs"
"Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs"
"Assets/_Project/Scripts/Gameplay/Floater.cs"
"Assets/_Project/Scripts/Gameplay/FloraProjectile.cs"
"Assets/_Project/Scripts/Gameplay/GravTrap.cs"
"Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs"
"Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs"
"Assets/_Project/Scripts/Gameplay/HarvestablePlant.cs"
"Assets/_Project/Scripts/Gameplay/HazardExposureNotifier.cs"
"Assets/_Project/Scripts/Gameplay/HazardMutationProfile.cs"
"Assets/_Project/Scripts/Gameplay/HazardType.cs"
"Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs"
"Assets/_Project/Scripts/Gameplay/HazardZoneProfile.cs"
"Assets/_Project/Scripts/Gameplay/HeavyTowWinch.cs"
"Assets/_Project/Scripts/Gameplay/HectonCameraState.cs"
"Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs"
"Assets/_Project/Scripts/Gameplay/HectonHazardSource.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerStateMachine.cs"
"Assets/_Project/Scripts/Gameplay/HectonScanRenderRegistry.cs"
"Assets/_Project/Scripts/Gameplay/HectonScannedRenderTarget.cs"
"Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs"
"Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs"
"Assets/_Project/Scripts/Gameplay/HostileFlora.cs"
"Assets/_Project/Scripts/Gameplay/IEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/IHectonPlayerEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/IHectonPlayerStateMachine.cs"
"Assets/_Project/Scripts/Gameplay/IKinematicVehicleTransportSource.cs"
"Assets/_Project/Scripts/Gameplay/IMotorForces.cs"
"Assets/_Project/Scripts/Gameplay/IPlayerTransportLifecycleOwner.cs"
"Assets/_Project/Scripts/Gameplay/IPlayerTransportSource.cs"
"Assets/_Project/Scripts/Gameplay/ISubmarineRuntimeContext.cs"
"Assets/_Project/Scripts/Gameplay/ITowSnapReceiver.cs"
"Assets/_Project/Scripts/Gameplay/ITransportPlatform.cs"
"Assets/_Project/Scripts/Gameplay/ItemHighlight.cs"
"Assets/_Project/Scripts/Gameplay/LifePodDamageSystem.cs"
"Assets/_Project/Scripts/Gameplay/LifePodFireExtinguisherNozzle.cs"
"Assets/_Project/Scripts/Gameplay/LifePodTactilePrologueController.cs"
"Assets/_Project/Scripts/Gameplay/MantaEmergencyWreck.cs"
"Assets/_Project/Scripts/Gameplay/MantaScooter.cs"
"Assets/_Project/Scripts/Gameplay/MessageTerminal.cs"
"Assets/_Project/Scripts/Gameplay/MeteorSplashQuadVfx.cs"
"Assets/_Project/Scripts/Gameplay/MissionData.cs"
"Assets/_Project/Scripts/Gameplay/MissionManager.cs"
"Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs"
"Assets/_Project/Scripts/Gameplay/OxygenBubble.cs"
"Assets/_Project/Scripts/Gameplay/OxygenPlant.cs"
"Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs"
"Assets/_Project/Scripts/Gameplay/PlayerActionController.cs"
"Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs"
"Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs"
"Assets/_Project/Scripts/Gameplay/PlayerExpressionProfile.cs"
"Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/PlayerMovementBrineRuntimeSystem.cs"
"Assets/_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.Body.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimMotor.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerToolSwimContract.cs"
"Assets/_Project/Scripts/Gameplay/PlayerToolSwimHandedness.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportBinder.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportFeelContract.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportOccupancyMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportOrientationMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportPreset.cs"
"Assets/_Project/Scripts/Gameplay/ProceduralFabrikArmJobs.cs"
"Assets/_Project/Scripts/Gameplay/RadiationHazard.cs"
"Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"
"Assets/_Project/Scripts/Gameplay/RandomEventMeteorMath.cs"
"Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs"
"Assets/_Project/Scripts/Gameplay/ResearchDirector.cs"
"Assets/_Project/Scripts/Gameplay/RuntimeSurvivalStats.cs"
"Assets/_Project/Scripts/Gameplay/SargassumCutResponder.cs"
"Assets/_Project/Scripts/Gameplay/SargassumMovementInfluence.cs"
"Assets/_Project/Scripts/Gameplay/SargassumPhysicsZone.cs"
"Assets/_Project/Scripts/Gameplay/ScannableFragment.cs"
"Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs"
"Assets/_Project/Scripts/Gameplay/SealedDoor.cs"
"Assets/_Project/Scripts/Gameplay/SolarPanel.cs"
"Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/SomaticSurvivalMath.cs"
"Assets/_Project/Scripts/Gameplay/StorageCrate.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineCompoundColliderAuthoring.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineProfile.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs"
"Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeResolver.cs"
"Assets/_Project/Scripts/Gameplay/SurvivalPhysiologyScalarJob.cs"
"Assets/_Project/Scripts/Gameplay/SurvivalStatusMasks.cs"
"Assets/_Project/Scripts/Gameplay/SwimPresentationProfile.cs"
"Assets/_Project/Scripts/Gameplay/SwimPresentationProfileLibrary.cs"
"Assets/_Project/Scripts/Gameplay/ToolEffectEvents.cs"
"Assets/_Project/Scripts/Gameplay/ToxinHazard.cs"
"Assets/_Project/Scripts/Gameplay/TransportChargingStation.cs"
"Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticRuntimeBootstrap.cs"
"Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs"
"Assets/_Project/Scripts/Gameplay/VehicleMotor.cs"
"Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs"
"Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs"
"Assets/_Project/Scripts/GlobalPhysicsStateManager.cs"
"Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs"
"Assets/_Project/Scripts/GravityTetherTool.cs"
"Assets/_Project/Scripts/HUDNotification.cs"
"Assets/_Project/Scripts/HUDQuickBar.cs"
"Assets/_Project/Scripts/HarpoonLauncherTool.cs"
"Assets/_Project/Scripts/HectonAtmosphereManager.cs"
"Assets/_Project/Scripts/HectonBiomeFamilyProfile.cs"
"Assets/_Project/Scripts/HectonBiomeLandmarkPlanProfile.cs"
"Assets/_Project/Scripts/HectonBiomeMatrixCatalog.cs"
"Assets/_Project/Scripts/HectonBiomeMatrixProfile.cs"
"Assets/_Project/Scripts/HectonBiomePlayProfile.cs"
"Assets/_Project/Scripts/HectonBiomeProfile.cs"
"Assets/_Project/Scripts/HectonBiomeRegistry.cs"
"Assets/_Project/Scripts/HectonBiomeResourceChannelProfile.cs"
"Assets/_Project/Scripts/HectonBiomeResourcePlanProfile.cs"
"Assets/_Project/Scripts/HectonBiomeSpatialPatternProfile.cs"
"Assets/_Project/Scripts/HectonBoidController.cs"
"Assets/_Project/Scripts/HectonCelestialEngine.cs"
"Assets/_Project/Scripts/HectonContactJob.cs"
"Assets/_Project/Scripts/HectonCrestOceanKinematics.cs"
"Assets/_Project/Scripts/HectonDirectorAI.cs"
"Assets/_Project/Scripts/HectonDiscoveryManager.cs"
"Assets/_Project/Scripts/HectonFabricatorUI.cs"
"Assets/_Project/Scripts/HectonFaunaFamilyProfile.cs"
"Assets/_Project/Scripts/HectonFloatingOrigin.cs"
"Assets/_Project/Scripts/HectonFluidEngine.cs"
"Assets/_Project/Scripts/HectonInventoryUI.cs"
"Assets/_Project/Scripts/HectonItem.cs"
"Assets/_Project/Scripts/HectonNarrativeDirector.cs"
"Assets/_Project/Scripts/HectonOceanPalette.cs"
"Assets/_Project/Scripts/HectonOceanRegistry.cs"
"Assets/_Project/Scripts/HectonPlayerMovement.cs"
"Assets/_Project/Scripts/HectonPlayerSpawner.cs"
"Assets/_Project/Scripts/HectonRockManager.cs"
"Assets/_Project/Scripts/HectonScanMarkerSystem.cs"
"Assets/_Project/Scripts/HectonSocketHelper.cs"
"Assets/_Project/Scripts/HectonSuitHUDExtensions.cs"
"Assets/_Project/Scripts/HectonSuitHUD_v4.cs"
"Assets/_Project/Scripts/HectonSurvivalSystem.cs"
"Assets/_Project/Scripts/HectonUnderwaterVisuals.cs"
"Assets/_Project/Scripts/HectonVoxelEngine.cs"
"Assets/_Project/Scripts/HectonVoxelVolume.cs"
"Assets/_Project/Scripts/HectonWorldGenerator.cs"
"Assets/_Project/Scripts/HydrationScheduler.cs"
"Assets/_Project/Scripts/IBuildPlacementRule.cs"
"Assets/_Project/Scripts/ICuttable.cs"
"Assets/_Project/Scripts/IFabricator.cs"
"Assets/_Project/Scripts/IHectonOceanKinematics.cs"
"Assets/_Project/Scripts/IOceanKinematics.cs"
"Assets/_Project/Scripts/IOriginShiftListener.cs"
"Assets/_Project/Scripts/IPoolable.cs"
"Assets/_Project/Scripts/IPowerComponent.cs"
"Assets/_Project/Scripts/ISaveable.cs"
"Assets/_Project/Scripts/ITickable.cs"
"Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs"
"Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs"
"Assets/_Project/Scripts/Interaction/HeavyCarryInteractable.cs"
"Assets/_Project/Scripts/Interaction/IInteractable.cs"
"Assets/_Project/Scripts/Interaction/IKinematicRepairTarget.cs"
"Assets/_Project/Scripts/Interaction/InteractableRegistry.cs"
"Assets/_Project/Scripts/Interaction/InteractionEvents.cs"
"Assets/_Project/Scripts/Interaction/InteractionUI.cs"
"Assets/_Project/Scripts/Interaction/InventoryPickupContracts.cs"
"Assets/_Project/Scripts/Interaction/KinematicTerminalInteractionBridge.cs"
"Assets/_Project/Scripts/Interaction/LifePodSeatStrapCoordinator.cs"
"Assets/_Project/Scripts/Interaction/LifePodSeatStrapLatch.cs"
"Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandController.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandReceiverRegistry.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandSide.cs"
"Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs"
"Assets/_Project/Scripts/Interaction/PhysicalSnapSwitch.cs"
"Assets/_Project/Scripts/Interaction/PhysicalToolGripOffsets.cs"
"Assets/_Project/Scripts/Interaction/PlayerInteraction.cs"
"Assets/_Project/Scripts/Interaction/SaveStation.cs"
"Assets/_Project/Scripts/Interaction/SuitDamageEvents.cs"
"Assets/_Project/Scripts/Interaction/VRCableDragPlug.cs"
"Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs"
"Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs"
"Assets/_Project/Scripts/InteractionHighlighter.cs"
"Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs"
"Assets/_Project/Scripts/Inventory/ItemPhysicalMetadata.cs"
"Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs"
"Assets/_Project/Scripts/Inventory/PressurizedContainer.cs"
"Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs"
"Assets/_Project/Scripts/InventoryEvents.cs"
"Assets/_Project/Scripts/InventoryGrid.cs"
"Assets/_Project/Scripts/ItemCatalog.cs"
"Assets/_Project/Scripts/ItemData.cs"
"Assets/_Project/Scripts/Items/PickupItem.cs"
"Assets/_Project/Scripts/KnifeTool.cs"
"Assets/_Project/Scripts/LandingImpactVFX.cs"
"Assets/_Project/Scripts/LaserCutter.cs"
"Assets/_Project/Scripts/LightDetectionSystem.cs"
"Assets/_Project/Scripts/LocKeys.Generated.cs"
"Assets/_Project/Scripts/LocNumericBuffer.cs"
"Assets/_Project/Scripts/LocRegistry.cs"
"Assets/_Project/Scripts/LocalizationEvents.cs"
"Assets/_Project/Scripts/LocalizationKeys.cs"
"Assets/_Project/Scripts/LocalizationManager.cs"
"Assets/_Project/Scripts/LocalizedAudioClipSet.cs"
"Assets/_Project/Scripts/LocalizedInlineIconResolver.cs"
"Assets/_Project/Scripts/LocalizedMeasurementFormatter.cs"
"Assets/_Project/Scripts/LocalizedSpriteRenderer.cs"
"Assets/_Project/Scripts/LocalizedTextReference.cs"
"Assets/_Project/Scripts/LocalizedWorldSign.cs"
"Assets/_Project/Scripts/LogicSpannerTool.cs"
"Assets/_Project/Scripts/MainMenuController.cs"
"Assets/_Project/Scripts/MainMenuInputRoutingGuard.cs"
"Assets/_Project/Scripts/MapMagicBridge.cs"
"Assets/_Project/Scripts/Meta/DifficultyModifierData.cs"
"Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs"
"Assets/_Project/Scripts/Meta/GlobalProfileData.cs"
"Assets/_Project/Scripts/Meta/GlobalProfileManager.cs"
"Assets/_Project/Scripts/Meta/MetaBuffInjector.cs"
"Assets/_Project/Scripts/Meta/MetaProfileUtility.cs"
"Assets/_Project/Scripts/Meta/MetaRuntimeInstaller.cs"
"Assets/_Project/Scripts/Meta/MetaUpgradeRegistry.cs"
"Assets/_Project/Scripts/Meta/RunModifierController.cs"
"Assets/_Project/Scripts/ModalWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/Editor/ModApiSandboxTunerWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/Editor/ModKernelInspectorWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonGameEvents.cs"
"Assets/_Project/Scripts/ModdingAPI/IHectonMod.cs"
"Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs"
"Assets/_Project/Scripts/ModdingAPI/IllegalContractException.cs"
"Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs"
"Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs"
"Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs"
"Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs"
"Assets/_Project/Scripts/ModdingAPI/ModLoader.cs"
"Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuModEntryView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuSettingSliderView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuSettingToggleView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMetadata.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRuntimeInfo.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs"
"Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs"
"Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs"
"Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs"
"Assets/_Project/Scripts/ModularEquipmentEngine.cs"
"Assets/_Project/Scripts/ModuleCatalog.cs"
"Assets/_Project/Scripts/ModuleMarker.cs"
"Assets/_Project/Scripts/ModuleSocket.cs"
"Assets/_Project/Scripts/ModuleStatusEvents.cs"
"Assets/_Project/Scripts/Narrative/ColonistLoreRegistry.cs"
"Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs"
"Assets/_Project/Scripts/Narrative/DeepReachCorporationData.cs"
"Assets/_Project/Scripts/Narrative/FaunaLoreRegistry.cs"
"Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs"
"Assets/_Project/Scripts/Narrative/LoreEncyclopediaLazyProxy.cs"
"Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs"
"Assets/_Project/Scripts/Narrative/NarrativeRuntimeInstaller.cs"
"Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs"
"Assets/_Project/Scripts/NarrativeDiscovery.cs"
"Assets/_Project/Scripts/NarrativeEvents.cs"
"Assets/_Project/Scripts/Networking/HectonNetworkManager.cs"
"Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs"
"Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs"
"Assets/_Project/Scripts/NoiseSystem.cs"
"Assets/_Project/Scripts/ObjectPoolDiagnostics.cs"
"Assets/_Project/Scripts/ObjectPoolManager.cs"
"Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs"
"Assets/_Project/Scripts/OmegaSurvivalKinematicsSmokeTester.cs"
"Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs"
"Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs"
"Assets/_Project/Scripts/Optimization/AssetRecord.cs"
"Assets/_Project/Scripts/Optimization/CameraRTManager.cs"
"Assets/_Project/Scripts/Optimization/GeneratedAssetGuidIdTable.cs"
"Assets/_Project/Scripts/Optimization/HardwareProfiler.cs"
"Assets/_Project/Scripts/Optimization/PostFXRTManager.cs"
"Assets/_Project/Scripts/Optimization/PreInitAssetIdMap.cs"
"Assets/_Project/Scripts/Optimization/RenderTextureAllocationRecord.cs"
"Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs"
"Assets/_Project/Scripts/Optimization/RenderTexturePool.cs"
"Assets/_Project/Scripts/Optimization/UIRTManager.cs"
"Assets/_Project/Scripts/Optimization/VRAMBudgetThresholds.cs"
"Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs"
"Assets/_Project/Scripts/Optimization/VRAMMonitor.cs"
"Assets/_Project/Scripts/Optimization/VRAMOptimizationBootstrap.cs"
"Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs"
"Assets/_Project/Scripts/Optimization/VisorRTManager.cs"
"Assets/_Project/Scripts/OriginShiftEventData.cs"
"Assets/_Project/Scripts/PDA/PDALogbookManager.cs"
"Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs"
"Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs"
"Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs"
"Assets/_Project/Scripts/PDA/PDAUtility.cs"
"Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs"
"Assets/_Project/Scripts/PDAInventoryTab.cs"
"Assets/_Project/Scripts/PerformanceMonitor.cs"
"Assets/_Project/Scripts/PersistentIDConverter.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementContracts.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/GlobalPhysicsStateManager.BuoyancyBridge.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/PhysicsApplySystem.BuoyancyQueue.cs"
"Assets/_Project/Scripts/Physics/CablePhysicsDebugGizmo132.cs"
"Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs"
"Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationContracts.cs"
"Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs"
"Assets/_Project/Scripts/Physics/Editor/HabitatFluidIncursionTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Exosuit/Editor/ExosuitKinematicsTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs"
"Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs"
"Assets/_Project/Scripts/Physics/FluidMathCore.cs"
"Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs"
"Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.WakeRequests.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionCsv.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs"
"Assets/_Project/Scripts/Physics/KCC/Editor/HydrodynamicKccTunerWindow.cs"
"Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs"
"Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs"
"Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs"
"Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs"
"Assets/_Project/Scripts/Physics/TetherBlackBoxDumpWriter.cs"
"Assets/_Project/Scripts/Physics/TetherSignals.cs"
"Assets/_Project/Scripts/Physics/TetherVerletJobs.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/Editor/SubmarineAutopilotTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs"
"Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs"
"Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs"
"Assets/_Project/Scripts/Physics/VerletCableDTOs.cs"
"Assets/_Project/Scripts/PhysicsApplySystem.cs"
"Assets/_Project/Scripts/PlacementGhost.cs"
"Assets/_Project/Scripts/PlayerBuilder.cs"
"Assets/_Project/Scripts/PlayerFlashlight.cs"
"Assets/_Project/Scripts/PlayerFootstepAudio.cs"
"Assets/_Project/Scripts/PlayerInventory.cs"
"Assets/_Project/Scripts/PlayerLocomotionMode.cs"
"Assets/_Project/Scripts/PlayerPDA.cs"
"Assets/_Project/Scripts/PlayerThrusterAudio.cs"
"Assets/_Project/Scripts/PlayerTool.cs"
"Assets/_Project/Scripts/PlayerToolManager.cs"
"Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs"
"Assets/_Project/Scripts/Power/PowerGridModuleData.cs"
"Assets/_Project/Scripts/Power/PowerGridTelemetryEvents.cs"
"Assets/_Project/Scripts/Power/PowerRelayNode.cs"
"Assets/_Project/Scripts/Power/ReactorCoreProfile.cs"
"Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs"
"Assets/_Project/Scripts/Power/SubmarineOsThermalGridGizmo.cs"
"Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs"
"Assets/_Project/Scripts/Power/WfcOutpostGridRegistry.cs"
"Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs"
"Assets/_Project/Scripts/PowerGrid.cs"
"Assets/_Project/Scripts/PowerGridManager.cs"
"Assets/_Project/Scripts/PowerNode.cs"
"Assets/_Project/Scripts/PrefabRegistry.cs"
"Assets/_Project/Scripts/ProceduralFamily_Fauna.cs"
"Assets/_Project/Scripts/ProfilerRegistry.cs"
"Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs"
"Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs"
"Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs"
"Assets/_Project/Scripts/Progression/ProgressionRuntimeInstaller.cs"
"Assets/_Project/Scripts/PropulsionTool.cs"
"Assets/_Project/Scripts/ProximityColliderSystem.cs"
"Assets/_Project/Scripts/QueryCacheContext.cs"
"Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs"
"Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs"
"Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs"
"Assets/_Project/Scripts/Quest/QuestDagMockSignalJobs.cs"
"Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs"
"Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs"
"Assets/_Project/Scripts/Quest/QuestData.cs"
"Assets/_Project/Scripts/Quest/QuestEvents.cs"
"Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs"
"Assets/_Project/Scripts/Quest/QuestManager.cs"
"Assets/_Project/Scripts/Quest/QuestRuntimeTypes.cs"
"Assets/_Project/Scripts/Quest/QuestStateManager.cs"
"Assets/_Project/Scripts/RTLProcessor.cs"
"Assets/_Project/Scripts/RaycastBatchHelper.cs"
"Assets/_Project/Scripts/RecipeData.cs"
"Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs"
"Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs"
"Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs"
"Assets/_Project/Scripts/Rendering/LutArrayResolver.cs"
"Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs"
"Assets/_Project/Scripts/RepairTool.cs"
"Assets/_Project/Scripts/ResourceNode.cs"
"Assets/_Project/Scripts/RockAttachmentData.cs"
"Assets/_Project/Scripts/RockDataLink.cs"
"Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs"
"Assets/_Project/Scripts/RuntimeInstanceId.cs"
"Assets/_Project/Scripts/RuntimePerformanceProfiler.cs"
"Assets/_Project/Scripts/SalvageSamplerTool.cs"
"Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs"
"Assets/_Project/Scripts/SaveBinaryStorage.cs"
"Assets/_Project/Scripts/SaveBinaryStorageNativeArrayExtensions.cs"
"Assets/_Project/Scripts/SaveData.cs"
"Assets/_Project/Scripts/SaveDataMigration.cs"
"Assets/_Project/Scripts/SaveDataMigration_AupV8.cs"
"Assets/_Project/Scripts/SaveEvents.cs"
"Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs"
"Assets/_Project/Scripts/SaveManager.cs"
"Assets/_Project/Scripts/SaveMetadata.cs"
"Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs"
"Assets/_Project/Scripts/SaveRecoverySmokeTester.cs"
"Assets/_Project/Scripts/SaveSidecarStorage.cs"
"Assets/_Project/Scripts/SaveSlotAuditResult.cs"
"Assets/_Project/Scripts/SaveSlotInfo.cs"
"Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs"
"Assets/_Project/Scripts/SaveSlotRepairResult.cs"
"Assets/_Project/Scripts/SaveSlotUI.cs"
"Assets/_Project/Scripts/SaveSystem/Editor/EntitySaveTunerWindow.cs"
"Assets/_Project/Scripts/SaveSystem/Editor/VoxelSaveTunerWindow.cs"
"Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs"
"Assets/_Project/Scripts/SaveSystem/EntityDeltaGizmoProbe.cs"
"Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs"
"Assets/_Project/Scripts/SaveSystem/H8WalInspector.cs"
"Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs"
"Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs"
"Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs"
"Assets/_Project/Scripts/SaveSystem/SteamCloudSaveConflictResolver.cs"
"Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs"
"Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs"
"Assets/_Project/Scripts/SaveThumbnailSystem.cs"
"Assets/_Project/Scripts/ScanEvents.cs"
"Assets/_Project/Scripts/ScanLogSystem.cs"
"Assets/_Project/Scripts/ScanRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/ScannableCategoryUtility.cs"
"Assets/_Project/Scripts/ScannableTarget.cs"
"Assets/_Project/Scripts/ScannerTool.cs"
"Assets/_Project/Scripts/ScatterBudgetController.cs"
"Assets/_Project/Scripts/ScavengePopulator.cs"
"Assets/_Project/Scripts/Scavenging/HarvestableTemplate.cs"
"Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs"
"Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs"
"Assets/_Project/Scripts/SeamGapDitherRenderer.cs"
"Assets/_Project/Scripts/SeamRegistry.cs"
"Assets/_Project/Scripts/SkySystemFollowCamera.cs"
"Assets/_Project/Scripts/SpatialAudioManager.cs"
"Assets/_Project/Scripts/StringBuilderPool.cs"
"Assets/_Project/Scripts/StunPistolTool.cs"
"Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs"
"Assets/_Project/Scripts/SubmarineElectrolysisModule.cs"
"Assets/_Project/Scripts/SubmarineFluidDynamics.cs"
"Assets/_Project/Scripts/SubmarineStructuralGrid.cs"
"Assets/_Project/Scripts/SuitData.cs"
"Assets/_Project/Scripts/SuitHUDProfile.cs"
"Assets/_Project/Scripts/SurfaceStateUtility.cs"
"Assets/_Project/Scripts/SurvivalKinematicsSmokeTester.cs"
"Assets/_Project/Scripts/SurvivalStats.cs"
"Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs"
"Assets/_Project/Scripts/TetherClass.cs"
"Assets/_Project/Scripts/TetherInstance.cs"
"Assets/_Project/Scripts/TetherManager.cs"
"Assets/_Project/Scripts/TetherProfileSO.cs"
"Assets/_Project/Scripts/ThermalGeyser.cs"
"Assets/_Project/Scripts/ThermalMeltSmokeTester.cs"
"Assets/_Project/Scripts/ThermalSurvivalSmokeTester.cs"
"Assets/_Project/Scripts/ThermalUpdraftVolume.cs"
"Assets/_Project/Scripts/ThreatCostTable.cs"
"Assets/_Project/Scripts/ToolHitUtility.cs"
"Assets/_Project/Scripts/ToolLoadoutProvisioner.cs"
"Assets/_Project/Scripts/ToolRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/ToolStagingSpawner.cs"
"Assets/_Project/Scripts/ToolTrialRangeRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Tools/EquipmentHardwareSpecsCsvParser.cs"
"Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs"
"Assets/_Project/Scripts/Tools/HapticWaveformLibrary.cs"
"Assets/_Project/Scripts/Tools/IBatteryTool.cs"
"Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs"
"Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs"
"Assets/_Project/Scripts/Tools/PerformanceMonitor.cs"
"Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs"
"Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs"
"Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs"
"Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs"
"Assets/_Project/Scripts/Tools/ToolLoadoutPreset.cs"
"Assets/_Project/Scripts/Tools/ToolMetadata.cs"
"Assets/_Project/Scripts/Tools/ToolModuleData.cs"
"Assets/_Project/Scripts/Tools/ToolUpgradeData.cs"
"Assets/_Project/Scripts/Tools/ToolUpgradeSystem.cs"
"Assets/_Project/Scripts/Tools/VerificationRuntimeProbe.cs"
"Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs"
"Assets/_Project/Scripts/UI/ARWaypointOverlay.cs"
"Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs"
"Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs"
"Assets/_Project/Scripts/UI/ActionProgressHUD.cs"
"Assets/_Project/Scripts/UI/AnalogGaugeNeedle3D.cs"
"Assets/_Project/Scripts/UI/AudioWaveformAnimator.cs"
"Assets/_Project/Scripts/UI/BIOSMessageStreamer.cs"
"Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs"
"Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs"
"Assets/_Project/Scripts/UI/BeaconHUDElement.cs"
"Assets/_Project/Scripts/UI/BlackBoxMetricDashboard.cs"
"Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs"
"Assets/_Project/Scripts/UI/CharBufferPool.cs"
"Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs"
"Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs"
"Assets/_Project/Scripts/UI/DiegeticHudTextNode.cs"
"Assets/_Project/Scripts/UI/DiegeticPDAController.cs"
"Assets/_Project/Scripts/UI/DiegeticPanelController.cs"
"Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs"
"Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs"
"Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs"
"Assets/_Project/Scripts/UI/EngineHealthOverlay.cs"
"Assets/_Project/Scripts/UI/FakeRadarBlipController.cs"
"Assets/_Project/Scripts/UI/FontAssetRecovery.cs"
"Assets/_Project/Scripts/UI/FontStreamingManager.cs"
"Assets/_Project/Scripts/UI/GhostSignalUtility.cs"
"Assets/_Project/Scripts/UI/GlitchEncoder.cs"
"Assets/_Project/Scripts/UI/GlitchTable.cs"
"Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs"
"Assets/_Project/Scripts/UI/HectonOSBootManager.cs"
"Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs"
"Assets/_Project/Scripts/UI/HectonTextNode.cs"
"Assets/_Project/Scripts/UI/HectonUIScaler.cs"
"Assets/_Project/Scripts/UI/HphiReactiveUiTelemetry.cs"
"Assets/_Project/Scripts/UI/HudNumericStringCache.cs"
"Assets/_Project/Scripts/UI/InteractionUI.cs"
"Assets/_Project/Scripts/UI/LabelSwapScheduler.cs"
"Assets/_Project/Scripts/UI/LoadingScreenController.cs"
"Assets/_Project/Scripts/UI/LoadingTipsDisplay.cs"
"Assets/_Project/Scripts/UI/LocOverflowHandler.cs"
"Assets/_Project/Scripts/UI/LocalizedFontResolver.cs"
"Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs"
"Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs"
"Assets/_Project/Scripts/UI/LocalizedTextMadnessFx.cs"
"Assets/_Project/Scripts/UI/MainMenuAudioIntegration.cs"
"Assets/_Project/Scripts/UI/NotificationEvents.cs"
"Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs"
"Assets/_Project/Scripts/UI/PDABarterTab.cs"
"Assets/_Project/Scripts/UI/PDAConstructionTab.cs"
"Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs"
"Assets/_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs"
"Assets/_Project/Scripts/UI/PDADataLogTab.cs"
"Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs"
"Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs"
"Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs"
"Assets/_Project/Scripts/UI/PDAIntrusionManager.cs"
"Assets/_Project/Scripts/UI/PDAInventoryFilterButton.cs"
"Assets/_Project/Scripts/UI/PDALoadoutTab.cs"
"Assets/_Project/Scripts/UI/PDAMapTab.cs"
"Assets/_Project/Scripts/UI/PDAShellChrome.cs"
"Assets/_Project/Scripts/UI/PDASpectrumTab.cs"
"Assets/_Project/Scripts/UI/PDATabButton.cs"
"Assets/_Project/Scripts/UI/PauseControlsPanel.cs"
"Assets/_Project/Scripts/UI/PauseMenuAudioIntegration.cs"
"Assets/_Project/Scripts/UI/PauseMenuController.cs"
"Assets/_Project/Scripts/UI/PauseMenuHost.cs"
"Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs"
"Assets/_Project/Scripts/UI/PhysicalPanelButton.cs"
"Assets/_Project/Scripts/UI/PhysicalPanelDial.cs"
"Assets/_Project/Scripts/UI/PhysicalTerminalKeyboard.cs"
"Assets/_Project/Scripts/UI/RelayHUDElement.cs"
"Assets/_Project/Scripts/UI/RelayHUDRuntimeBootstrap.cs"
"Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs"
"Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs"
"Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs"
"Assets/_Project/Scripts/UI/SettingsComparisonView.cs"
"Assets/_Project/Scripts/UI/SettingsLivePreview.cs"
"Assets/_Project/Scripts/UI/SettingsManager.cs"
"Assets/_Project/Scripts/UI/SettingsPanel.cs"
"Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs"
"Assets/_Project/Scripts/UI/SettingsPanelProfiler.cs"
"Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs"
"Assets/_Project/Scripts/UI/SonarHoloCompass.cs"
"Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs"
"Assets/_Project/Scripts/UI/SubnauticaSystemsDebugUI.cs"
"Assets/_Project/Scripts/UI/SubtitleManager.cs"
"Assets/_Project/Scripts/UI/SuitAdvisoryController.cs"
"Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs"
"Assets/_Project/Scripts/UI/SurvivalHUDController.cs"
"Assets/_Project/Scripts/UI/TMP_TextRegistry.cs"
"Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs"
"Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs"
"Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs"
"Assets/_Project/Scripts/UI/UIAudioFeedback.cs"
"Assets/_Project/Scripts/UI/UIButtonAudioTrigger.cs"
"Assets/_Project/Scripts/UI/UIFadeTransition.cs"
"Assets/_Project/Scripts/UI/UIParticleEffect.cs"
"Assets/_Project/Scripts/UI/UIScreenShake.cs"
"Assets/_Project/Scripts/UI/UISliderValueDisplay.cs"
"Assets/_Project/Scripts/UI/UITooltip.cs"
"Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs"
"Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs"
"Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs"
"Assets/_Project/Scripts/UIRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/VFX/BiomeProfile.cs"
"Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs"
"Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs"
"Assets/_Project/Scripts/VFX/NativeTrailRenderer.cs"
"Assets/_Project/Scripts/VFX/ShakeProfile.cs"
"Assets/_Project/Scripts/VFX/VFXEmissionProfile.cs"
"Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs"
"Assets/_Project/Scripts/VFX/VolumetricFogContracts.cs"
"Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs"
"Assets/_Project/Scripts/VFX/Wakes/WakeDisplacementData.cs"
"Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs"
"Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs"
"Assets/_Project/Scripts/Visor/DeferredDecalPass.cs"
"Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs"
"Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs"
"Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs"
"Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs"
"Assets/_Project/Scripts/Visor/Editor/ScreenSpaceDecalTunerWindow.cs"
"Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs"
"Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiosDiagnosticState.cs"
"Assets/_Project/Scripts/Visor/HectonDrsRenderFeatureGate.cs"
"Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs"
"Assets/_Project/Scripts/Visor/HectonDryVolumeStencilSource.cs"
"Assets/_Project/Scripts/Visor/HectonFillrateDepthPrepassFeature.cs"
"Assets/_Project/Scripts/Visor/HectonFlashlightVoxelShadowProvider.cs"
"Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs"
"Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs"
"Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs"
"Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs"
"Assets/_Project/Scripts/Visor/HectonOverdrawHeatmapFeature.cs"
"Assets/_Project/Scripts/Visor/HectonRetinaDistortionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs"
"Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs"
"Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVRDiegeticFocusController.cs"
"Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs"
"Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs"
"Assets/_Project/Scripts/Visor/PlayerStressVFX.cs"
"Assets/_Project/Scripts/Visor/SonarGridOverlay.cs"
"Assets/_Project/Scripts/Visor/SpectrumSystem.cs"
"Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs"
"Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs"
"Assets/_Project/Scripts/Visor/VisorHUDController.cs"
"Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs"
"Assets/_Project/Scripts/VisualBudgetSmokeTester.cs"
"Assets/_Project/Scripts/VisualCascadeSmokeTester.cs"
"Assets/_Project/Scripts/VisualOmegaSmokeTester.cs"
"Assets/_Project/Scripts/VortexVolume.cs"
"Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs"
"Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs"
"Assets/_Project/Scripts/VoxelDeltaPersistenceDTO.cs"
"Assets/_Project/Scripts/VoxelDeltaProcessor.cs"
"Assets/_Project/Scripts/VoxelRuntimeIntegrityUtility.cs"
"Assets/_Project/Scripts/VoxelSeamDirector.cs"
"Assets/_Project/Scripts/World/AUPMath.cs"
"Assets/_Project/Scripts/World/AbsoluteUniversePositionBlit.cs"
"Assets/_Project/Scripts/World/AbyssalFluidDecalManager.cs"
"Assets/_Project/Scripts/World/AbyssalThermalManager.cs"
"Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs"
"Assets/_Project/Scripts/World/BasePollutionManager.cs"
"Assets/_Project/Scripts/World/BioCableIK.cs"
"Assets/_Project/Scripts/World/Biolum/CaveBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/FloorBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/OceanBiolumZone.cs"
"Assets/_Project/Scripts/World/BiomeMatrixSmokeTester.cs"
"Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs"
"Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfJobs.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntimeBootstrap.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs"
"Assets/_Project/Scripts/World/Biomes/Editor/BiomeTransitionTunerWindow.cs"
"Assets/_Project/Scripts/World/BoidStructValidator.cs"
"Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs"
"Assets/_Project/Scripts/World/ChunkLocalOffsetQuantization.cs"
"Assets/_Project/Scripts/World/CrestDepthCacheDebugger.cs"
"Assets/_Project/Scripts/World/CrestFoamDebugger.cs"
"Assets/_Project/Scripts/World/CullingManager.cs"
"Assets/_Project/Scripts/World/DepthZoneDirector.cs"
"Assets/_Project/Scripts/World/DepthZoneProfile.cs"
"Assets/_Project/Scripts/World/DestructibleOrganicManager.cs"
"Assets/_Project/Scripts/World/DispatcherJobSwap.cs"
"Assets/_Project/Scripts/World/DropBuffer.cs"
"Assets/_Project/Scripts/World/DynamicResolutionScaler.cs"
"Assets/_Project/Scripts/World/EcosystemBalanceProfile.cs"
"Assets/_Project/Scripts/World/EcosystemDirector.cs"
"Assets/_Project/Scripts/World/EcosystemEnvelope.cs"
"Assets/_Project/Scripts/World/Editor/AbyssalScentTunerWindow.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelay.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs"
"Assets/_Project/Scripts/World/EntropyYieldJob.cs"
"Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs"
"Assets/_Project/Scripts/World/ErosionHarnessJobs.cs"
"Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs"
"Assets/_Project/Scripts/World/FloraBrain.cs"
"Assets/_Project/Scripts/World/FloraDataTemplate.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeCsvHotloader.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs"
"Assets/_Project/Scripts/World/FloraInteractionManager.cs"
"Assets/_Project/Scripts/World/FloraRegrowthDirector.cs"
"Assets/_Project/Scripts/World/GPR/GroundRadarJobs.cs"
"Assets/_Project/Scripts/World/GPUScatterDirector.cs"
"Assets/_Project/Scripts/World/GeneticTraitProfile.cs"
"Assets/_Project/Scripts/World/GlobalWorldSampler.cs"
"Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs"
"Assets/_Project/Scripts/World/HLODInstance.cs"
"Assets/_Project/Scripts/World/HectonAnomalyBrineJobs.cs"
"Assets/_Project/Scripts/World/HectonAnomalyEngine.cs"
"Assets/_Project/Scripts/World/HectonAnomalyFeatureJobs.cs"
"Assets/_Project/Scripts/World/HectonAnomalyResourceBinding.cs"
"Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs"
"Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs"
"Assets/_Project/Scripts/World/HectonBiolumController.cs"
"Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs"
"Assets/_Project/Scripts/World/HectonBrineToxicMudGrid.cs"
"Assets/_Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs"
"Assets/_Project/Scripts/World/HectonCaveVoxelLightingVolume.cs"
"Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs"
"Assets/_Project/Scripts/World/HectonHLODRenderer.cs"
"Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs"
"Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs"
"Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs"
"Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorData.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorTypes.cs"
"Assets/_Project/Scripts/World/HectonProceduralVegetationStripBuilder.cs"
"Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs"
"Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs"
"Assets/_Project/Scripts/World/HectonSpatialHash.cs"
"Assets/_Project/Scripts/World/HectonVegetationConstants.cs"
"Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs"
"Assets/_Project/Scripts/World/HectonWorldStreamingTypes.cs"
"Assets/_Project/Scripts/World/HydraulicErosionJob.cs"
"Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs"
"Assets/_Project/Scripts/World/ISargassumMassiveDisplacementReceiver.cs"
"Assets/_Project/Scripts/World/ImpostorSystem.cs"
"Assets/_Project/Scripts/World/InstancedFloraRenderer.cs"
"Assets/_Project/Scripts/World/LODSystemManager.cs"
"Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"
"Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs"
"Assets/_Project/Scripts/World/ProceduralFamily_Fauna.cs"
"Assets/_Project/Scripts/World/ProceduralFamily_Flora.cs"
"Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs"
"Assets/_Project/Scripts/World/ProxyLightRegistry.cs"
"Assets/_Project/Scripts/World/ResourceDistributionDirector.cs"
"Assets/_Project/Scripts/World/ResourceYieldMath.cs"
"Assets/_Project/Scripts/World/SamplingSnapshot.cs"
"Assets/_Project/Scripts/World/SargassumCollapseChunk.cs"
"Assets/_Project/Scripts/World/SargassumCrestDampingController.cs"
"Assets/_Project/Scripts/World/SargassumCutManager.cs"
"Assets/_Project/Scripts/World/SargassumDebrisParticleSystem.cs"
"Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs"
"Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs"
"Assets/_Project/Scripts/World/ScatterBackendBindingBridge.cs"
"Assets/_Project/Scripts/World/ScatterBackendBindingState.cs"
"Assets/_Project/Scripts/World/ScatterBackendParityReference.cs"
"Assets/_Project/Scripts/World/ScatterBackendRequestFactory.cs"
"Assets/_Project/Scripts/World/ScatterBackendRuntimeHost.cs"
"Assets/_Project/Scripts/World/ScatterBackendRuntimeStatus.cs"
"Assets/_Project/Scripts/World/ScatterBackendScheduleRequest.cs"
"Assets/_Project/Scripts/World/ScatterBackendShadowCompletion.cs"
"Assets/_Project/Scripts/World/ScatterBackendSupportContext.cs"
"Assets/_Project/Scripts/World/ScatterCandidateEvaluator.cs"
"Assets/_Project/Scripts/World/ScatterClassicBackendAdapters.cs"
"Assets/_Project/Scripts/World/ScatterDiagnosticsTracker.cs"
"Assets/_Project/Scripts/World/ScatterEvaluationEngine.cs"
"Assets/_Project/Scripts/World/ScatterEvaluator.cs"
"Assets/_Project/Scripts/World/ScatterGPUIBackend.cs"
"Assets/_Project/Scripts/World/ScatterHeuristicsUtility.cs"
"Assets/_Project/Scripts/World/ScatterHybridRuntimeEntryPoint.cs"
"Assets/_Project/Scripts/World/ScatterInstancingService.cs"
"Assets/_Project/Scripts/World/ScatterMath.cs"
"Assets/_Project/Scripts/World/ScatterRebuildProfileSnapshot.cs"
"Assets/_Project/Scripts/World/ScatterReconcileMetrics.cs"
"Assets/_Project/Scripts/World/ScatterRuntimeBackendFacade.cs"
"Assets/_Project/Scripts/World/SedimentAccumulationManager.cs"
"Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs"
"Assets/_Project/Scripts/World/SoundscapeSystem.cs"
"Assets/_Project/Scripts/World/SpatialSonarSnapshot.cs"
"Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs"
"Assets/_Project/Scripts/World/TectonicActivityProfile.cs"
"Assets/_Project/Scripts/World/ThermalSlumpingJob.cs"
"Assets/_Project/Scripts/World/VegetationCapacityUtilities.cs"
"Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs"
"Assets/_Project/Scripts/World/VegetationDensityQueryService.cs"
"Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs"
"Assets/_Project/Scripts/World/VegetationMath.cs"
"Assets/_Project/Scripts/World/VegetationMemoryPool.cs"
"Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs"
"Assets/_Project/Scripts/World/VegetationPersistenceManager.cs"
"Assets/_Project/Scripts/World/VegetationPredatorFearField.cs"
"Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs"
"Assets/_Project/Scripts/World/VegetationThermalSampler.cs"
"Assets/_Project/Scripts/World/VegetationThreatAndStructureService.cs"
"Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs"
"Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs"
"Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs"
"Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs"
"Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntimeLifecycle.cs"
"Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs"
"Assets/_Project/Scripts/World/WorldGenRegistrySmokeTester.cs"
"Assets/_Project/Scripts/World/WorldGenerativeGeologyTelemetry.cs"
"Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs"
"Assets/_Project/Scripts/World/WorldPickupStateCodec.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainFakeOverhangJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainTectonicDisplacementJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainTerraceJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs"
"Assets/_Project/Scripts/World/WorldReadabilityDirector.cs"
"Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs"
"Assets/_Project/Scripts/World/WorldShippingContentFilter.cs"
"Assets/_Project/Scripts/World/WorldShippingSceneRuntimeGuard.cs"
"Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs"
"Assets/_Project/Scripts/World/WorldVolumetricBiomeClassificationJobs.cs"
"Assets/_Project/Scripts/World/WreckMaterialRegistry.cs"
"Assets/_Project/Scripts/WorldCaveDirector.cs"
"Assets/_Project/Scripts/WorldChunkCoordinate.cs"
"Assets/_Project/Scripts/WorldChunkStreamingProfile.cs"
"Assets/_Project/Scripts/WorldContentDirector.cs"
"Assets/_Project/Scripts/WorldContentProfile.cs"
"Assets/_Project/Scripts/WorldContentSocket.cs"
"Assets/_Project/Scripts/WorldExpeditionLoopProfile.cs"
"Assets/_Project/Scripts/WorldFaunaSpawnRegistry.cs"
"Assets/_Project/Scripts/WorldFidelityRoot.cs"
"Assets/_Project/Scripts/WorldGeneratedPrimitiveFactory.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyMeshBuilder.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyProfile.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologySeamPlan.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyService.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBlendRequest.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs"
"Assets/_Project/Scripts/WorldInterestAnchor.cs"
"Assets/_Project/Scripts/WorldInterestDirector.cs"
"Assets/_Project/Scripts/WorldMacroZoneCoordinate.cs"
"Assets/_Project/Scripts/WorldMotivationProfile.cs"
"Assets/_Project/Scripts/WorldPopulationDirector.cs"
"Assets/_Project/Scripts/WorldPopulationRule.cs"
"Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs"
"Assets/_Project/Scripts/WorldProceduralBiomeFamilyContextCatalog.cs"
"Assets/_Project/Scripts/WorldProceduralBiomeFamilyContextProfile.cs"
"Assets/_Project/Scripts/WorldProceduralClusterFocus.cs"
"Assets/_Project/Scripts/WorldProceduralFaunaMood.cs"
"Assets/_Project/Scripts/WorldProceduralFieldSampler.cs"
"Assets/_Project/Scripts/WorldProceduralFillDirector.cs"
"Assets/_Project/Scripts/WorldProceduralPattern.cs"
"Assets/_Project/Scripts/WorldProceduralPatternCatalog.cs"
"Assets/_Project/Scripts/WorldProceduralPatternProfile.cs"
"Assets/_Project/Scripts/WorldProceduralPlaceholderMarker.cs"
"Assets/_Project/Scripts/WorldProceduralPlacementRule.cs"
"Assets/_Project/Scripts/WorldProceduralProxyInstance.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirector.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorDiagnosticsContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorEnvironmentalEnvelope.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorPlacementRetentionContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorPlacementTypes.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorReconcileContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorRescueContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorRuntimeStateContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSpatialHelpers.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSpawnBatchContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs"
"Assets/_Project/Scripts/WorldProceduralStateRegistry.cs"
"Assets/_Project/Scripts/WorldProceduralStructureFocus.cs"
"Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs"
"Assets/_Project/Scripts/WorldSandboxAttractionProfile.cs"
"Assets/_Project/Scripts/WorldSliceAnchor.cs"
"Assets/_Project/Scripts/WorldSliceDirector.cs"
"Assets/_Project/Scripts/WorldStateManager.cs"
"Assets/_Project/Scripts/WorldStreamingDirector.cs"
"Assets/_Project/Scripts/WorldStreamingLayer.cs"
"Assets/_Project/Scripts/WorldZoneAnchor.cs"
"Assets/_Project/Scripts/WorldZoneDirector.cs"
"Assets/_Project/Scripts/WorldZonePlanProfile.cs"
"Assets/_Project/Scripts/WorldZoneProfile.cs"
"Assets/_Project/Scripts/ZeroGCStringCache.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
[3124/3439 3s] ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.MockDomain.Runtime.dll (+pdb)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\Compilation\Unity.ILPP.Trigger\Unity.ILPP.Trigger.exe" @"Library\Bee\artifacts\rsp\12719471298722492838.rsp"
Contents of Library\Bee\artifacts\rsp\12719471298722492838.rsp
"unity-ilpp-7964abe555f4ec2c1439bad844e00f5c" p "Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll" "Library/Bee/artifacts/1900b0aEDbg.dag/post-processed" "UNITY_6000_4_1" "UNITY_6000_4" "UNITY_6000" "UNITY_5_3_OR_NEWER" "UNITY_5_4_OR_NEWER" "UNITY_5_5_OR_NEWER" "UNITY_5_6_OR_NEWER" "UNITY_2017_1_OR_NEWER" "UNITY_2017_2_OR_NEWER" "UNITY_2017_3_OR_NEWER" "UNITY_2017_4_OR_NEWER" "UNITY_2018_1_OR_NEWER" "UNITY_2018_2_OR_NEWER" "UNITY_2018_3_OR_NEWER" "UNITY_2018_4_OR_NEWER" "UNITY_2019_1_OR_NEWER" "UNITY_2019_2_OR_NEWER" "UNITY_2019_3_OR_NEWER" "UNITY_2019_4_OR_NEWER" "UNITY_2020_1_OR_NEWER" "UNITY_2020_2_OR_NEWER" "UNITY_2020_3_OR_NEWER" "UNITY_2021_1_OR_NEWER" "UNITY_2021_2_OR_NEWER" "UNITY_2021_3_OR_NEWER" "UNITY_2022_1_OR_NEWER" "UNITY_2022_2_OR_NEWER" "UNITY_2022_3_OR_NEWER" "UNITY_2023_1_OR_NEWER" "UNITY_2023_2_OR_NEWER" "UNITY_2023_3_OR_NEWER" "UNITY_6000_0_OR_NEWER" "UNITY_6000_1_OR_NEWER" "UNITY_6000_2_OR_NEWER" "UNITY_6000_3_OR_NEWER" "UNITY_6000_4_OR_NEWER" "PLATFORM_ARCH_64" "UNITY_64" "UNITY_INCLUDE_TESTS" "ENABLE_AR" "ENABLE_AUDIO" "ENABLE_AUDIO_SCRIPTABLE_PIPELINE" "ENABLE_CACHING" "ENABLE_CLOTH" "ENABLE_EVENT_QUEUE" "ENABLE_MICROPHONE" "ENABLE_MULTIPLE_DISPLAYS" "ENABLE_PHYSICS" "ENABLE_TEXTURE_STREAMING" "ENABLE_VIRTUALTEXTURING" "ENABLE_LZMA" "ENABLE_UNITYEVENTS" "ENABLE_VR" "ENABLE_WEBCAM" "ENABLE_UNITYWEBREQUEST" "ENABLE_WWW" "ENABLE_CLOUD_SERVICES" "ENABLE_CLOUD_SERVICES_ADS" "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST" "ENABLE_UNITY_CONSENT" "ENABLE_UNITY_CLOUD_IDENTIFIERS" "ENABLE_CLOUD_SERVICES_CRASH_REPORTING" "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING" "ENABLE_CLOUD_SERVICES_PURCHASING" "ENABLE_CLOUD_SERVICES_ANALYTICS" "ENABLE_CLOUD_SERVICES_BUILD" "ENABLE_EDITOR_GAME_SERVICES" "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT" "ENABLE_CLOUD_LICENSE" "ENABLE_EDITOR_HUB_LICENSE" "ENABLE_WEBSOCKET_CLIENT" "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API" "ENABLE_DIRECTOR_AUDIO" "ENABLE_DIRECTOR_TEXTURE" "ENABLE_MANAGED_JOBS" "ENABLE_MANAGED_TRANSFORM_JOBS" "ENABLE_MANAGED_ANIMATION_JOBS" "ENABLE_MANAGED_AUDIO_JOBS" "ENABLE_MANAGED_UNITYTLS" "INCLUDE_DYNAMIC_GI" "ENABLE_SCRIPTING_GC_WBARRIERS" "PLATFORM_SUPPORTS_MONO" "RENDER_SOFTWARE_CURSOR" "ENABLE_MARSHALLING_TESTS" "ENABLE_VIDEO" "ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK" "ENABLE_ACCELERATOR_CLIENT_DEBUGGING" "ENABLE_ACCESSIBILITY_SCREEN_READER" "TEXTCORE_1_0_OR_NEWER" "EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED" "PLATFORM_STANDALONE_WIN" "PLATFORM_STANDALONE" "UNITY_STANDALONE_WIN" "UNITY_STANDALONE" "ENABLE_RUNTIME_GI" "ENABLE_MOVIES" "ENABLE_NETWORK" "ENABLE_NVIDIA" "ENABLE_AMD" "ENABLE_CRUNCH_TEXTURE_COMPRESSION" "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS" "ENABLE_OUT_OF_PROCESS_CRASH_HANDLER" "ENABLE_CLUSTER_SYNC" "ENABLE_CLUSTERINPUT" "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP" "GFXDEVICE_WAITFOREVENT_MESSAGEPUMP" "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER" "PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION" "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS" "ENABLE_MONO" "NET_STANDARD_2_0" "NET_STANDARD" "NET_STANDARD_2_1" "NETSTANDARD" "NETSTANDARD2_1" "ENABLE_PROFILER" "ENABLE_PROFILER_ASSISTANT_INTEGRATION" "DEBUG" "TRACE" "UNITY_ASSERTIONS" "UNITY_EDITOR" "UNITY_EDITOR_64" "UNITY_EDITOR_WIN" "ENABLE_UNITY_COLLECTIONS_CHECKS" "ENABLE_BURST_AOT" "UNITY_TEAM_LICENSE" "ENABLE_CUSTOM_RENDER_TEXTURE" "ENABLE_DIRECTOR" "ENABLE_LOCALIZATION" "ENABLE_SPRITES" "ENABLE_TERRAIN" "ENABLE_TILEMAP" "ENABLE_TIMELINE" "ENABLE_INPUT_SYSTEM" "TEXTCORE_FONT_ENGINE_1_5_OR_NEWER" "TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER" "TEXTCORE_FONT_ENGINE_1_6_OR_NEWER" "DOTWEEN" "CREST_OCEAN" "CREST_URP" "__MICROSPLAT__" "MAPMAGIC2" "MM_NATIVE" "UNITY_VISUAL_SCRIPTING" "GPU_INSTANCER" "ODIN_INSPECTOR" "ODIN_INSPECTOR_3" "ODIN_INSPECTOR_3_1" "AMPLIFY_SHADER_EDITOR" "SHAPES_URP" "MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED" "BAKERY_INCLUDED" "VLB_URP" "ODIN_INSPECTOR_3_2" "ODIN_INSPECTOR_3_3" "H8_BURST_FUNCTION_POINTERS" "CSHARP_7_OR_LATER" "CSHARP_7_3_OR_NEWER" -r "Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Global.Contracts.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.MockDomain.Contracts.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Unity.Burst.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Unity.Mathematics.dll" "Assets\AstarPathfindingProject\Plugins\Clipper\Pathfinding.ClipperLib.dll" "Assets\AstarPathfindingProject\Plugins\DotNetZip\Pathfinding.Ionic.Zip.Reduced.dll" "Assets\AstarPathfindingProject\Plugins\Poly2Tri\Pathfinding.Poly2Tri.dll" "Assets\Candice AI for Games\Scripts\Libs\Candice Save System\Plugins\Mono.Data.Sqlite.dll" "Assets\MeshBaker\Libs\MeshBakerEditorLib.dll" "Assets\MeshBaker\Libs\MeshBakerLib.dll" "Assets\Plugins\Demigiant\DOTween\DOTween.dll" "Assets\Plugins\Demigiant\DOTween\Editor\DOTweenEditor.dll" "Assets\Plugins\Demigiant\DOTweenPro\DOTweenPro.dll" "Assets\Plugins\Demigiant\DOTweenPro\Editor\DOTweenProEditor.dll" "Assets\Plugins\Demigiant\DemiLib\Core\DemiLib.dll" "Assets\Plugins\Demigiant\DemiLib\Core\Editor\DemiEditor.dll" "Assets\Plugins\Editor\RelationsInspector\RelationsInspector.dll" "Assets\Plugins\Roslyn\Microsoft.CodeAnalysis.CSharp.dll" "Assets\Plugins\Roslyn\Microsoft.CodeAnalysis.dll" "Assets\Plugins\Roslyn\System.Collections.Immutable.dll" "Assets\Plugins\Roslyn\System.Reflection.Metadata.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.OdinInspector.Attributes.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.OdinInspector.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Reflection.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Serialization.Config.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Serialization.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Utilities.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Utilities.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\Extensions\2.0.0\System.Runtime.InteropServices.WindowsRuntime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.ComponentModel.Composition.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Core.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Data.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Drawing.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.IO.Compression.FileSystem.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Net.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Numerics.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Runtime.Serialization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.ServiceModel.Web.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Transactions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Web.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Windows.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.Linq.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.Serialization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\Microsoft.Win32.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.AppContext.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Buffers.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.Concurrent.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.NonGeneric.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.Specialized.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.EventBasedAsync.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.TypeConverter.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Console.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Data.Common.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Contracts.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Debug.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.FileVersionInfo.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Process.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.StackTrace.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.TextWriterTraceListener.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Tools.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.TraceSource.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Tracing.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Drawing.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Dynamic.Runtime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.Calendars.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Compression.ZipFile.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Compression.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.DriveInfo.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.Watcher.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.IsolatedStorage.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.MemoryMappedFiles.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Pipes.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.UnmanagedMemoryStream.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Expressions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Parallel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Queryable.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Memory.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Http.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.NameResolution.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.NetworkInformation.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Ping.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Requests.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Security.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Sockets.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebHeaderCollection.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebSockets.Client.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebSockets.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Numerics.Vectors.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ObjectModel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.DispatchProxy.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.ILGeneration.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.Lightweight.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.Reader.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.ResourceManager.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.Writer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.CompilerServices.VisualC.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Handles.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.InteropServices.RuntimeInformation.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.InteropServices.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Numerics.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Formatters.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Json.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Xml.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Claims.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Algorithms.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Csp.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Encoding.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.X509Certificates.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Principal.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.SecureString.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.Encoding.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.Encoding.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.RegularExpressions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Overlapped.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.Parallel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Thread.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.ThreadPool.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Timer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ValueTuple.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.ReaderWriter.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XPath.XDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XPath.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XmlDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XmlSerializer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\Unity.Android.Gradle.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\Unity.Android.Types.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\MacStandaloneSupport\UnityEditor.iOS.Extensions.Xcode.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\Unity.Plastic.Antlr3.Runtime.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\Unity.Plastic.Newtonsoft.Json.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\log4netPlastic.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\unityplastic.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.LowLevel.ILSupport\Unity.Collections.LowLevel.ILSupport.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.Tests\System.IO.Hashing\System.IO.Hashing.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.Tests\System.Runtime.CompilerServices.Unsafe\System.Runtime.CompilerServices.Unsafe.dll" "Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll" "Library\PackageCache\com.unity.nuget.mono-cecil@ecb9724e46ff\Mono.Cecil.dll" "Library\PackageCache\com.unity.nuget.newtonsoft-json@4dfd81071c64\Runtime\Newtonsoft.Json.dll" "Library\PackageCache\com.unity.sharp-zip-lib@f6e4ef34e4d8\Runtime\Unity.SharpZipLib.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\Dependencies\DotNetZip\Unity.VisualScripting.IonicZip.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\Dependencies\YamlDotNet\Unity.VisualScripting.YamlDotNet.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\EditorAssetResources\Unity.VisualScripting.TextureAssets.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Runtime\VisualScripting.Flow\Dependencies\NCalc\Unity.VisualScripting.Antlr3.Runtime.dll"
ExitCode
-1073740791
Output
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
*** Tundra build failed (8.08 seconds), 6 items updated, 3439 evaluated
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
Scripts have compiler errors.
Exiting without bug reporter. Application will terminate with return code 1
===== END FILE: Unity_SHINOBU_160_compile.log =====

===== FILE: Unity_SHINOBU_160_compile_after_hotpath.log =====
[Licensing::Module] Trying to connect to existing licensing client channel...
Built from '6000.4/staging' branch; Version is '6000.4.1f1 (8535861f39e1) revision 8729990'; Using compiler version '194234433'; Build Type 'Release'
OS: 'Windows 11 (10.0.26200) CoreSingleLanguage' Language: 'en' Physical Memory: 32407 MB
[Licensing::IpcConnector] Channel LicenseClient-danat doesn't exist
BatchMode: 1, IsHumanControllingUs: 0, StartBugReporterOnCrash: 0, Is64bit: 1
System architecture: x64
Process architecture: x64
Date: 2026-05-19T21:11:13Z
[Licensing::Module] Successfully launched LicensingClient (PId: 27936)
COMMAND LINE ARGUMENTS:
C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe
-batchmode
-nographics
-quit
-projectPath
C:\hades\Hecton8
-logFile
C:\hades\Hecton8\Docs\AgentLogs\Unity_SHINOBU_160_compile_after_hotpath.log
Successfully changed project path to: C:\hades\Hecton8
C:/hades/Hecton8
[UnityMemory] Configuration Parameters - Can be set up in boot.config
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-nav-mesh-worker=65536"
"memorysetup-temp-allocator-size-audio-worker=65536"
"memorysetup-temp-allocator-size-cloud-worker=32768"
"memorysetup-temp-allocator-size-gfx=262144"
"memorysetup-temp-allocator-size-preload-manager=33554432"
"memorysetup-temp-allocator-size-job-worker=262144"
"memorysetup-temp-allocator-size-background-worker=32768"
"memorysetup-allocator-temp-initial-block-size-main=262144"
"memorysetup-allocator-temp-initial-block-size-worker=262144"
"memorysetup-bucket-allocator-granularity=16"
"memorysetup-bucket-allocator-bucket-count=8"
"memorysetup-bucket-allocator-block-size=33554432"
"memorysetup-bucket-allocator-block-count=8"
"memorysetup-main-allocator-block-size=16777216"
"memorysetup-thread-allocator-block-size=16777216"
"memorysetup-gfx-main-allocator-block-size=16777216"
"memorysetup-gfx-thread-allocator-block-size=16777216"
"memorysetup-cache-allocator-block-size=4194304"
"memorysetup-typetree-allocator-block-size=2097152"
"memorysetup-profiler-bucket-allocator-granularity=16"
"memorysetup-profiler-bucket-allocator-bucket-count=8"
"memorysetup-profiler-bucket-allocator-block-size=33554432"
"memorysetup-profiler-bucket-allocator-block-count=8"
"memorysetup-profiler-allocator-block-size=16777216"
"memorysetup-profiler-editor-allocator-block-size=1048576"
"memorysetup-temp-allocator-size-main=16777216"
"memorysetup-job-temp-allocator-block-size=2097152"
"memorysetup-job-temp-allocator-block-size-background=1048576"
"memorysetup-job-temp-allocator-reduction-small-platforms=262144"
Player connection [22772] Target information:
Player connection [22772] * "[IP] 192.168.1.130 [Port] 55504 [Flags] 2 [Guid] 2575839589 [EditorId] 2575839589 [Version] 1048832 [Id] WindowsEditor(7,Shinobu) [Debug] 1 [PackageName] WindowsEditor [ProjectName] Editor"
Player connection [22772] * "[IP] 10.77.0.2 [Port] 55504 [Flags] 2 [Guid] 2575839589 [EditorId] 2575839589 [Version] 1048832 [Id] WindowsEditor(7,Shinobu) [Debug] 1 [PackageName] WindowsEditor [ProjectName] Editor"
Player connection [22772] Host joined multi-casting on [225.0.0.222:54997]...
Player connection [22772] Host joined alternative multi-casting on [225.0.0.222:34997]...
Input System module state changed to: Initialized.
[Physics::Module] Initialized fallback backend.
[Physics::Module] Id: 0xdecafbad
[Licensing::IpcConnector] Successfully connected to: "LicenseClient-danat" at "2026-05-19T21:11:14.6842059Z"
[Package Manager] Connected to IPC stream "Upm-29376" after 1.4 seconds.
[Licensing::Module] Licensing is not yet initialized.
[Licensing::Client] Handshaking with LicensingClient:
Version: 1.18.1+9fbee8e
Session Id: be58ff242f9d49bba07292163ed750fc
Correlation Id: 3210173a9366b7b9b32859268eae9106
External correlation Id: 476216246619605228
Machine Id: KXBg4HkLZwVfPhjJrzyzSmUVWFw=
[Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-danat" (connect: 1.31s, validation: 0.09s, handshake: 2.15s)
[Licensing::IpcConnector] Successfully connected to: "LicenseClient-danat-notifications" at "2026-05-19T21:11:16.9208595Z"
[Licensing::Module] Connected to LicensingClient (PId: 27936, launch time: 0.00, total connection time: 3.55s)
[Licensing::Module] Error: Access token is unavailable; failed to update
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Module] License group:
Id: 7972536317136-UnityPersXXXX
Product: Unity Personal
Type: Assigned
Expiration: Unlimited
[Licensing::Client] Successfully updated license, isAsync: True, time: 0.02
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Module] Licensing Background thread has ended after 3.60s
[Licensing::Module] Licensing is initialized (took 1.66s).
[Licensing::Client] Successfully resolved entitlement details
Library Redirect Path: Library/
[Physics::Module] Selected backend.
[Physics::Module] Name: PhysX
[Physics::Module] Id: 0xf2b8ea05
[Physics::Module] SDK Version: 4.1.2
[Physics::Module] Integration Version: 1.0.0
[Physics::Module] Threading Mode: Multi-Threaded
Refreshing native plugins compatible for Editor in 299.57 ms, found 27 plugins.
Initialize engine version: 6000.4.1f1 (8535861f39e1)
[Subsystems] Discovering subsystems at path C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Resources/UnitySubsystems
[Subsystems] Discovering subsystems at path C:/hades/Hecton8/Assets
Forcing GfxDevice: Null
GfxDevice: creating device client; kGfxThreadingModeNonThreaded
NullGfxDevice:
Version: NULL 1.0 [1.0]
Renderer: Null Device
Vendor: Unity Technologies
Initialize mono
Mono path[0] = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed'
Mono path[1] = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/MonoBleedingEdge/lib/mono/unityjit-win32'
Mono config path = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/MonoBleedingEdge/etc'
Using monoOptions --debugger-agent=transport=dt_socket,embedding=1,server=y,suspend=n,address=127.0.0.1:56376
CodeReloadManager initialized
Using cacheserver namespaces - metadata:defaultmetadata, artifacts:defaultartifacts
Using cacheserver namespaces - metadata:defaultmetadata, artifacts:defaultartifacts
ImportWorker Server TCP listen port: 0
AcceleratorClientConnectionCallback - disconnected - :0
Begin MonoManager ReloadAssembly
Registering precompiled unity dll's ...
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll
[Licensing::Client] Successfully resolved entitlement details
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll
Registered in 0.007568 seconds.
Native extension for LinuxStandalone target not found
Native extension for Android target not found
Native extension for WindowsStandalone target not found
Native extension for OSXStandalone target not found
Package Manager log level set to [2]
[Licensing::Client] Successfully resolved entitlement details
ScheduleIndexationOnStartup MainProcess:False IndexOnStartup:True
Mono: successfully reloaded assembly
Finished resetting current domain, in 5.707 seconds
Domain Reload Profiling: 6924ms
BeginReloadAssembly (450ms)
CreateAndSetChildDomain (12ms)
RebuildCommonClasses (83ms)
RebuildNativeTypeToScriptingClass (20ms)
initialDomainReloadingComplete (129ms)
LoadAllAssembliesAndSetupDomain (533ms)
LoadAssemblies (433ms)
AnalyzeDomain (511ms)
TypeCache.Refresh (508ms)
TypeCache.ScanAssembly (482ms)
FinalizeReload (5710ms)
SetupLoadedEditorAssemblies (0ms)
InitializePlatformSupportModulesInManaged (103ms)
BeforeProcessingInitializeOnLoad (126ms)
ProcessInitializeOnLoadAttributes (211ms)
ProcessInitializeOnLoadMethodAttributes (5137ms)
[Licensing::Client] Successfully resolved entitlement details
Application.AssetDatabase Initial Refresh Start
[Package Manager] Restoring resolved packages state from cache
[Licensing::Client] Successfully resolved entitlement details
[Package Manager] Registered 74 packages:
Packages from [https://packages.unity.com]:
com.unity.ai.navigation@2.0.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ai.navigation@78534c21b27d)
com.unity.addressables@2.7.6 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.addressables@45e9abf44299)
com.unity.collab-proxy@2.11.4 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.collab-proxy@a5329f833fa8)
com.unity.inputsystem@1.19.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.inputsystem@21a28c3a6c83)
com.unity.memoryprofiler@1.1.12 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.memoryprofiler@485b5ba42ef5)
com.unity.nuget.newtonsoft-json@3.2.2 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.nuget.newtonsoft-json@4dfd81071c64)
com.unity.probuilder@6.0.9 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.probuilder@1f279ab829b7)
com.unity.sharp-zip-lib@1.4.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.sharp-zip-lib@f6e4ef34e4d8)
com.unity.timeline@1.8.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.timeline@bfd27f8016ff)
com.unity.visualscripting@1.9.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.visualscripting@8bed5ad90189)
com.unity.xr.management@4.6.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.management@ca5b202bb583)
com.unity.xr.meta-openxr@2.5.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.meta-openxr@dae986a05b5c)
com.unity.xr.openxr@1.17.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.openxr@5dc08d6a3e5b)
com.unity.searcher@4.9.4 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.searcher@d45a78918735)
com.unity.xr.core-utils@2.5.3 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.core-utils@f0450cbac8d6)
com.unity.xr.legacyinputhelpers@3.0.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.legacyinputhelpers@3f62d634f63b)
com.unity.xr.arfoundation@6.5.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.arfoundation@13d2457b468b)
com.unity.xr.compositionlayers@2.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.xr.compositionlayers@461024b1d757)
com.unity.settings-manager@2.1.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.settings-manager@0b8638c5ce86)
com.unity.burst@1.8.28 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.burst@07790c2d06d9)
com.unity.mathematics@1.3.3 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.mathematics@19a9377c4ffa)
com.unity.profiling.core@1.0.3 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.profiling.core@8a49f7027d06)
com.unity.editorcoroutines@1.0.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.editorcoroutines@54394ed3283c)
com.unity.scriptablebuildpipeline@2.6.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.scriptablebuildpipeline@36e3b5898ee2)
com.unity.nuget.mono-cecil@1.11.6 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.nuget.mono-cecil@ecb9724e46ff)
com.unity.test-framework.performance@3.2.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.test-framework.performance@0840f58e4562)
Built-in packages:
com.unity.2d.sprite@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.2d.sprite@929df5adbb1f)
com.unity.render-pipelines.universal@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.universal@580a03820d50)
com.unity.ugui@2.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ugui@d8a2716f3013)
com.unity.modules.accessibility@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.accessibility)
com.unity.modules.ai@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.ai)
com.unity.modules.androidjni@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.androidjni)
com.unity.modules.animation@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.animation)
com.unity.modules.assetbundle@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.assetbundle)
com.unity.modules.audio@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.audio)
com.unity.modules.cloth@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.cloth)
com.unity.modules.director@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.director)
com.unity.modules.imageconversion@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.imageconversion)
com.unity.modules.imgui@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.imgui)
com.unity.modules.jsonserialize@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.jsonserialize)
com.unity.modules.particlesystem@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.particlesystem)
com.unity.modules.physics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.physics)
com.unity.modules.physics2d@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.physics2d)
com.unity.modules.screencapture@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.screencapture)
com.unity.modules.terrain@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.terrain)
com.unity.modules.terrainphysics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.terrainphysics)
com.unity.modules.tilemap@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.tilemap)
com.unity.modules.ui@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.ui)
com.unity.modules.uielements@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.uielements)
com.unity.modules.umbra@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.umbra)
com.unity.modules.unityanalytics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unityanalytics)
com.unity.modules.unitywebrequest@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequest)
com.unity.modules.unitywebrequestassetbundle@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestassetbundle)
com.unity.modules.unitywebrequestaudio@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestaudio)
com.unity.modules.unitywebrequesttexture@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequesttexture)
com.unity.modules.unitywebrequestwww@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestwww)
com.unity.modules.vectorgraphics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vectorgraphics)
com.unity.modules.vehicles@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vehicles)
com.unity.modules.video@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.video)
com.unity.modules.vr@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vr)
com.unity.modules.wind@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.wind)
com.unity.modules.xr@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.xr)
com.unity.render-pipelines.core@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.core@e6c93b445dd3)
com.unity.modules.subsystems@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.subsystems)
com.unity.modules.hierarchycore@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.hierarchycore)
com.unity.render-pipelines.universal-config@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.universal-config@0db4263b9e6b)
com.unity.collections@6.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.collections@538ace9075bc)
com.unity.test-framework@1.6.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.test-framework@76560ee600cb)
com.unity.ext.nunit@2.0.5 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ext.nunit@d8c07649098d)
Embedded packages:
com.jbooth.microsplat.core@file:C:\hades\Hecton8\Packages\com.jbooth.microsplat.core (location: C:\hades\Hecton8\Packages\com.jbooth.microsplat.core)
com.jbooth.microsplat.urp2022@file:C:\hades\Hecton8\Packages\com.jbooth.microsplat.urp2022 (location: C:\hades\Hecton8\Packages\com.jbooth.microsplat.urp2022)
com.unity.shadergraph@file:C:\hades\Hecton8\Packages\com.unity.shadergraph (location: C:\hades\Hecton8\Packages\com.unity.shadergraph)
com.waveharmonic.crest@file:C:\hades\Hecton8\Packages\com.waveharmonic.crest (location: C:\hades\Hecton8\Packages\com.waveharmonic.crest)
Git packages:
com.coplaydev.unity-mcp@https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta (location: C:\hades\Hecton8\Library\PackageCache\com.coplaydev.unity-mcp@fbdb152757bd)
[Subsystems] Looking for new subsystems at path C:\hades\Hecton8\Library\PackageCache\com.unity.xr.meta-openxr@dae986a05b5c
[Subsystems] Novel subsystem found at Library/PackageCache/com.unity.xr.meta-openxr@dae986a05b5c/Runtime/UnitySubsystemsManifest.json
[Subsystems] Discovering subsystems at path C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Resources/UnitySubsystems
[Subsystems] Discovering subsystems at path C:/hades/Hecton8/Assets
[Subsystems] Discovering subsystems at path C:\hades\Hecton8\Library\PackageCache\com.unity.xr.meta-openxr@dae986a05b5c
[Subsystems] No descriptors matched for examples in Library/PackageCache/com.unity.xr.meta-openxr@dae986a05b5c/Runtime/UnitySubsystemsManifest.json.
[Subsystems] No descriptors matched for inputs in Library/PackageCache/com.unity.xr.meta-openxr@dae986a05b5c/Runtime/UnitySubsystemsManifest.json.
[Subsystems] No descriptors matched for displays in Library/PackageCache/com.unity.xr.meta-openxr@dae986a05b5c/Runtime/UnitySubsystemsManifest.json.
[Subsystems] 1 'meshings' descriptors matched in Library/PackageCache/com.unity.xr.meta-openxr@dae986a05b5c/Runtime/UnitySubsystemsManifest.json
[Subsystems] Discovering subsystems at path C:\hades\Hecton8\Library\PackageCache\com.unity.xr.openxr@5dc08d6a3e5b
[Subsystems] No descriptors matched for examples in Library/PackageCache/com.unity.xr.openxr@5dc08d6a3e5b/Runtime/UnitySubsystemsManifest.json.
[Subsystems] 1 'inputs' descriptors matched in Library/PackageCache/com.unity.xr.openxr@5dc08d6a3e5b/Runtime/UnitySubsystemsManifest.json
[Subsystems] 1 'displays' descriptors matched in Library/PackageCache/com.unity.xr.openxr@5dc08d6a3e5b/Runtime/UnitySubsystemsManifest.json
[Subsystems] No descriptors matched for meshings in Library/PackageCache/com.unity.xr.openxr@5dc08d6a3e5b/Runtime/UnitySubsystemsManifest.json.
[Subsystems] Discovering subsystems at path C:\hades\Hecton8\Library\PackageCache\com.unity.xr.arfoundation@13d2457b468b
[Subsystems] No descriptors matched for examples in Library/PackageCache/com.unity.xr.arfoundation@13d2457b468b/Runtime/UnitySubsystemsManifest.json.
[Subsystems] 1 'inputs' descriptors matched in Library/PackageCache/com.unity.xr.arfoundation@13d2457b468b/Runtime/UnitySubsystemsManifest.json
[Subsystems] No descriptors matched for displays in Library/PackageCache/com.unity.xr.arfoundation@13d2457b468b/Runtime/UnitySubsystemsManifest.json.
[Subsystems] 1 'meshings' descriptors matched in Library/PackageCache/com.unity.xr.arfoundation@13d2457b468b/Runtime/UnitySubsystemsManifest.json
[Package Manager] Done registering packages in 0.04 seconds
[ScriptCompilation] Requested script compilation because: AssetDatabase observed changes in script compilation related files
Starting: C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\bee_backend.exe --ipc --defer-dag-verification --dagfile="Library/Bee/1900b0aEDbg.dag" --continue-on-failure --profile="Library/Bee/backend1.traceevents" ScriptAssemblies
WorkingDir: C:/hades/Hecton8
DisplayProgressbar: Compiling Scripts
ExitCode: 3 Duration: 12s
[2420/3439 1s] ILPP-Configuration Library/ilpp-configuration.nevergeneratedoutput
[BUSY 6s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
[3120/3439 8s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Core.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:UNITY_ADDRESSABLES_EXIST
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/GPUInstancer.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Ecology.Migration.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Animation.IK.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Echolocation.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Propagation.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Virtualization.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Audio.Virtualization.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Bootstrap.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Cartography.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Bucketing.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Database.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Persistence.Paging.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Scheduling.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Environment.Fluids.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Environment.Fluids.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Input.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Algorithms.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Corrosion.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Inventory.Corrosion.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.Grid.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.Grid.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Logistics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.CCD.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.Determinism.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Physics.Tethers.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.UI.Diegetic.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Vehicles.Physics.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.Terrain.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Addressables.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.InputSystem.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Profiling.Core.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.RenderPipelines.Core.Runtime.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.RenderPipelines.Universal.Runtime.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.ResourceManager.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.TextMeshPro.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs"
"Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs"
"Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs"
"Assets/_Project/Scripts/AI/Perception/RetinalAdaptationVault.cs"
"Assets/_Project/Scripts/AI/Perception/RetinalExposureMath.cs"
"Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs"
"Assets/_Project/Scripts/AcousticZoneController.cs"
"Assets/_Project/Scripts/AmbientWaterMotion.cs"
"Assets/_Project/Scripts/AmbientWaterMotionManager.cs"
"Assets/_Project/Scripts/AmbientWaterMotionProfile.cs"
"Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs"
"Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs"
"Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs"
"Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs"
"Assets/_Project/Scripts/AssemblyInfo.cs"
"Assets/_Project/Scripts/AsyncLoadHelper.cs"
"Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs"
"Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"
"Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs"
"Assets/_Project/Scripts/Atmosphere/AtmosphericLightingState.cs"
"Assets/_Project/Scripts/Atmosphere/BaseAtmosphereEngine.cs"
"Assets/_Project/Scripts/Atmosphere/BaseAtmosphereMath.cs"
"Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs"
"Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuAtmosphereWaveTunerWindow.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs"
"Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherMath.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherProfile.cs"
"Assets/_Project/Scripts/Atmosphere/SurfaceWeatherVfxRig.cs"
"Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs"
"Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs"
"Assets/_Project/Scripts/AtmosphereProfile.cs"
"Assets/_Project/Scripts/Audio/AcousticReverbPresetTrigger.cs"
"Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs"
"Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs"
"Assets/_Project/Scripts/Audio/AudioMaterialProfile.cs"
"Assets/_Project/Scripts/Audio/DeepPsychosisController.cs"
"Assets/_Project/Scripts/Audio/Editor/AbyssalAcousticsTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/AdaptiveAudioTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/AudioImportDictator.cs"
"Assets/_Project/Scripts/Audio/Editor/AudioOmegaAutonomySmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/DSPThreadSafetySmokeTester.cs"
"Assets/_Project/Scripts/Audio/Editor/GranularSynthTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/SabineReverbDspTunerWindow.cs"
"Assets/_Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs"
"Assets/_Project/Scripts/Audio/HectonMusicBiomeProfile.cs"
"Assets/_Project/Scripts/Audio/HectonMusicClip.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirector.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirectorAnchor.cs"
"Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs"
"Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs"
"Assets/_Project/Scripts/Audio/MusicVoicePool.cs"
"Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalMetallicGrainBank.cs"
"Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs"
"Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs"
"Assets/_Project/Scripts/Audio/VocalWarningSystem.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogData.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogDiscoveryBitMask.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs"
"Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs"
"Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs"
"Assets/_Project/Scripts/AutomationSmokeTester.cs"
"Assets/_Project/Scripts/BarterRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BaseModule.cs"
"Assets/_Project/Scripts/BaseModuleTemplate.cs"
"Assets/_Project/Scripts/BaseStressRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BeaconDeployerTool.cs"
"Assets/_Project/Scripts/BeaconNetworkSystem.cs"
"Assets/_Project/Scripts/BeaconRuntime.cs"
"Assets/_Project/Scripts/BiomeDiscoveryBitMask.cs"
"Assets/_Project/Scripts/BiomeMatrixDirector.cs"
"Assets/_Project/Scripts/BiomeSamplerCache.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapController.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapHealthMonitor.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapRegistryCycleValidator.cs"
"Assets/_Project/Scripts/Bootstrap/BootstrapRouteEnforcer.cs"
"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"
"Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs"
"Assets/_Project/Scripts/Bootstrap/SceneGuard.cs"
"Assets/_Project/Scripts/Bootstrap/SceneInstantiationGate.cs"
"Assets/_Project/Scripts/Build/BuildInfo.cs"
"Assets/_Project/Scripts/Build/BuildInfoHudPresenter.cs"
"Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs"
"Assets/_Project/Scripts/BuildableData.cs"
"Assets/_Project/Scripts/BuilderRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/BuilderTool.cs"
"Assets/_Project/Scripts/BuoyancyObject.cs"
"Assets/_Project/Scripts/BuoyancyProfile.cs"
"Assets/_Project/Scripts/CameraJuiceProcessor.cs"
"Assets/_Project/Scripts/CaveBioRootsGenerator.cs"
"Assets/_Project/Scripts/CaveBiomeTemplate.cs"
"Assets/_Project/Scripts/CaveDressingConfig.cs"
"Assets/_Project/Scripts/CaveFaunaContext.cs"
"Assets/_Project/Scripts/CaveGlowingTissueRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveGraphGenerator.cs"
"Assets/_Project/Scripts/CaveRuntimeBoundsUtility.cs"
"Assets/_Project/Scripts/CaveSedimentShelfRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveServiceRemnantRuntimeBuilder.cs"
"Assets/_Project/Scripts/CaveTypes.cs"
"Assets/_Project/Scripts/CaveWallGrowthRuntimeBuilder.cs"
"Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs"
"Assets/_Project/Scripts/Compatibility/LegacyStubs/DefaultFlowFieldProfile.cs"
"Assets/_Project/Scripts/ComponentCache.cs"
"Assets/_Project/Scripts/Construction/AutomataTemplate.cs"
"Assets/_Project/Scripts/Construction/AutonomousExtractorJobs.cs"
"Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs"
"Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs"
"Assets/_Project/Scripts/Construction/BaseLogisticsNetwork.cs"
"Assets/_Project/Scripts/Construction/BaseModuleNavModifier.cs"
"Assets/_Project/Scripts/Construction/BatteryBankModule.cs"
"Assets/_Project/Scripts/Construction/BatteryChargerModule.cs"
"Assets/_Project/Scripts/Construction/BotanyPlanterModule.cs"
"Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs"
"Assets/_Project/Scripts/Construction/ConstructionSignals.cs"
"Assets/_Project/Scripts/Construction/CultivationManager.cs"
"Assets/_Project/Scripts/Construction/DeepDrillModule.cs"
"Assets/_Project/Scripts/Construction/DroneCognitionJob.cs"
"Assets/_Project/Scripts/Construction/DroneFleetManager.cs"
"Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs"
"Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs"
"Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs"
"Assets/_Project/Scripts/Construction/HabitatGraphManager.cs"
"Assets/_Project/Scripts/Construction/HabitatStressJobs.cs"
"Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeRoutingKernel.cs"
"Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs"
"Assets/_Project/Scripts/Construction/LogisticsRouteScratchMemory.cs"
"Assets/_Project/Scripts/Construction/LogisticsSorterModule.cs"
"Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs"
"Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs"
"Assets/_Project/Scripts/Construction/ModuleIntegrityComponent.cs"
"Assets/_Project/Scripts/Construction/ModuleLifeSupportComponent.cs"
"Assets/_Project/Scripts/Construction/RepairDroneEntity.cs"
"Assets/_Project/Scripts/Construction/RepairDroneHub.cs"
"Assets/_Project/Scripts/Construction/RepairStation.cs"
"Assets/_Project/Scripts/Construction/StructuralIntegrityProfile.cs"
"Assets/_Project/Scripts/Construction/TransitionHatchMeshState.cs"
"Assets/_Project/Scripts/Construction/VRConstructionWeldTarget.cs"
"Assets/_Project/Scripts/Construction/VRPipeBlueprintPreview.cs"
"Assets/_Project/Scripts/Construction/VehicleDockingModule.cs"
"Assets/_Project/Scripts/Construction/WaterPumpModule.cs"
"Assets/_Project/Scripts/ConstructionManager.cs"
"Assets/_Project/Scripts/ControlScheme.cs"
"Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs"
"Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs"
"Assets/_Project/Scripts/Core/Bridge/Generated/H8DesignFacadeContracts.generated.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeBinaryLayoutVerifier.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs"
"Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs"
"Assets/_Project/Scripts/Core/Bridge/H8DesignDataFacade.cs"
"Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs"
"Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs"
"Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs"
"Assets/_Project/Scripts/Core/BurstCallback.cs"
"Assets/_Project/Scripts/Core/CameraJuiceSignals.cs"
"Assets/_Project/Scripts/Core/CinematicMath.cs"
"Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs"
"Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs"
"Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs"
"Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs"
"Assets/_Project/Scripts/Core/Content/ContentSaveSlotTopology.cs"
"Assets/_Project/Scripts/Core/Content/ObjectBatchBase.cs"
"Assets/_Project/Scripts/Core/Content/VisibilityProxyBase.cs"
"Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs"
"Assets/_Project/Scripts/Core/Data/H8DataBaker.cs"
"Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs"
"Assets/_Project/Scripts/Core/Data/H8StaticDataSanity.cs"
"Assets/_Project/Scripts/Core/Data/InventoryCost.cs"
"Assets/_Project/Scripts/Core/Data/StaticDataStore.cs"
"Assets/_Project/Scripts/Core/DependencyAttribute.cs"
"Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs"
"Assets/_Project/Scripts/Core/DeterministicReplaySeed.cs"
"Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyePdaCommandConsole.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/Editor/ArchitectEyeBlackBoxTimelineViewer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs"
"Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultProbeUtility.cs"
"Assets/_Project/Scripts/Core/DispatcherJobFence.cs"
"Assets/_Project/Scripts/Core/DistanceMath.cs"
"Assets/_Project/Scripts/Core/DodReplayRecorder.cs"
"Assets/_Project/Scripts/Core/Editor/InputCurveHapticsTunerWindow.cs"
"Assets/_Project/Scripts/Core/EnumFastComparer.cs"
"Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs"
"Assets/_Project/Scripts/Core/FixedCharBuffer.cs"
"Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs"
"Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs"
"Assets/_Project/Scripts/Core/GCMonitor.cs"
"Assets/_Project/Scripts/Core/GameStartContext.cs"
"Assets/_Project/Scripts/Core/Generated/H8Hashes.cs"
"Assets/_Project/Scripts/Core/Generated/H8LoreHashes.cs"
"Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs"
"Assets/_Project/Scripts/Core/GlobalRegistry.cs"
"Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"
"Assets/_Project/Scripts/Core/GlobalSignals.cs"
"Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs"
"Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs"
"Assets/_Project/Scripts/Core/H8Debug.cs"
"Assets/_Project/Scripts/Core/HardwareProfileCatalog.cs"
"Assets/_Project/Scripts/Core/HardwareTierDetector.cs"
"Assets/_Project/Scripts/Core/HectonArenaAllocator.cs"
"Assets/_Project/Scripts/Core/HectonLayerMasks.cs"
"Assets/_Project/Scripts/Core/HectonNativeBridge.cs"
"Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs"
"Assets/_Project/Scripts/Core/HectonShadowBudgetLight.cs"
"Assets/_Project/Scripts/Core/HectonSpatialIntrinsics.cs"
"Assets/_Project/Scripts/Core/HectonThreadPriorityPolicy.cs"
"Assets/_Project/Scripts/Core/HectonUrpShadowBudgetGuard.cs"
"Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs"
"Assets/_Project/Scripts/Core/HectonXRManager.cs"
"Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs"
"Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs"
"Assets/_Project/Scripts/Core/HomeostasisBrain.cs"
"Assets/_Project/Scripts/Core/IDispatcherRaycastReceiver.cs"
"Assets/_Project/Scripts/Core/IOceanVisualBridge.cs"
"Assets/_Project/Scripts/Core/IPlatformIntegration.cs"
"Assets/_Project/Scripts/Core/InputDeterminismDtos.cs"
"Assets/_Project/Scripts/Core/InputDispatcher.cs"
"Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs"
"Assets/_Project/Scripts/Core/JobAdmissionTelemetryBridge.cs"
"Assets/_Project/Scripts/Core/JobFenceManager.cs"
"Assets/_Project/Scripts/Core/LogisticsPipeBuilder.cs"
"Assets/_Project/Scripts/Core/MacroDatabaseSignalBridge.cs"
"Assets/_Project/Scripts/Core/MaterialPropertyBlockRegistry.cs"
"Assets/_Project/Scripts/Core/MathGuard.cs"
"Assets/_Project/Scripts/Core/MemoryBudgetTracker.cs"
"Assets/_Project/Scripts/Core/MemoryInquisitor.cs"
"Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs"
"Assets/_Project/Scripts/Core/NativeAllocationTrackerRuntimeBridge.cs"
"Assets/_Project/Scripts/Core/NativeArenaAllocator.cs"
"Assets/_Project/Scripts/Core/NativeArenaArray.cs"
"Assets/_Project/Scripts/Core/NativeBitmask256.cs"
"Assets/_Project/Scripts/Core/NativeMemorySentinel.cs"
"Assets/_Project/Scripts/Core/NativeMemoryTrackingBridgeInstaller.cs"
"Assets/_Project/Scripts/Core/NativeQuery.cs"
"Assets/_Project/Scripts/Core/NativeRingBuffer.cs"
"Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs"
"Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs"
"Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs"
"Assets/_Project/Scripts/Core/PlatformAdaptiveBudgetGovernor.cs"
"Assets/_Project/Scripts/Core/PlatformBatteryWatchdog.cs"
"Assets/_Project/Scripts/Core/PlatformPrecisionClock.cs"
"Assets/_Project/Scripts/Core/PlayerInputState.cs"
"Assets/_Project/Scripts/Core/PlayerInventoryManager.cs"
"Assets/_Project/Scripts/Core/PlayerLookTargetPromptCache.cs"
"Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs"
"Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs"
"Assets/_Project/Scripts/Core/PlayerSensoryManager.cs"
"Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs"
"Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs"
"Assets/_Project/Scripts/Core/RebindingManager.cs"
"Assets/_Project/Scripts/Core/RegistryBucket.cs"
"Assets/_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs"
"Assets/_Project/Scripts/Core/RuntimeWatchdog.cs"
"Assets/_Project/Scripts/Core/SceneRuntimeService.cs"
"Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs"
"Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs"
"Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs"
"Assets/_Project/Scripts/Core/Signals/SignalCorridorMockSignalGenerators.cs"
"Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs"
"Assets/_Project/Scripts/Core/StackQueue.cs"
"Assets/_Project/Scripts/Core/SteamDeckInputPal.cs"
"Assets/_Project/Scripts/Core/SteamDeckRadialMenu.cs"
"Assets/_Project/Scripts/Core/SystemDispatcher.cs"
"Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs"
"Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs"
"Assets/_Project/Scripts/Core/UIStateStore.cs"
"Assets/_Project/Scripts/Core/UnsafeArenaAllocator.cs"
"Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs"
"Assets/_Project/Scripts/Core/VRAMBudgetTracker.cs"
"Assets/_Project/Scripts/Core/VoxelUnsafeExtensions.cs"
"Assets/_Project/Scripts/Core/ZeroGCFormatter.cs"
"Assets/_Project/Scripts/CraftingEvents.cs"
"Assets/_Project/Scripts/CraftingRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/CraftingSystem.cs"
"Assets/_Project/Scripts/CrashTelemetryBuffer.cs"
"Assets/_Project/Scripts/CreatureArchetypeData.cs"
"Assets/_Project/Scripts/CurrentManager.cs"
"Assets/_Project/Scripts/CurrentVolume.cs"
"Assets/_Project/Scripts/Data/BiomeContentPackContract.cs"
"Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs"
"Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs"
"Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs"
"Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs"
"Assets/_Project/Scripts/Data/ResearchDataTemplate.cs"
"Assets/_Project/Scripts/Data/ScannerUpgradeTemplate.cs"
"Assets/_Project/Scripts/Data/XenoBiologyTree.cs"
"Assets/_Project/Scripts/DemoDoor.cs"
"Assets/_Project/Scripts/DemoFirstPersonController.cs"
"Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs"
"Assets/_Project/Scripts/Dev/BotController.cs"
"Assets/_Project/Scripts/Dev/CelestialCataclysmSmokeTester.cs"
"Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs"
"Assets/_Project/Scripts/Dev/EditorPlayModeDiagnostics.cs"
"Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs"
"Assets/_Project/Scripts/Dev/IL2CPPCrashTelemetryDebugMenu.cs"
"Assets/_Project/Scripts/Dev/NarrativeProgressionSmokeTester.cs"
"Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs"
"Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Economy/EconomyInflationProfile.cs"
"Assets/_Project/Scripts/Economy/EconomyRuntimeInstaller.cs"
"Assets/_Project/Scripts/Economy/LootTable.cs"
"Assets/_Project/Scripts/Economy/RecyclingRegistry.cs"
"Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs"
"Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs"
"Assets/_Project/Scripts/Economy/ResourceStack.cs"
"Assets/_Project/Scripts/Economy/ScrapManager.cs"
"Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs"
"Assets/_Project/Scripts/Ecosystem/CreatureGeneticsProfile.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemMigrationProfile.cs"
"Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs"
"Assets/_Project/Scripts/Ecosystem/Editor/MacroEcosystemTunerWindow.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaBiomeMutationDefinition.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaBrain.Ecosystem.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGeneticTraits.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs"
"Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs"
"Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs"
"Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs"
"Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs"
"Assets/_Project/Scripts/EncounterDirector.cs"
"Assets/_Project/Scripts/EncounterProfile.cs"
"Assets/_Project/Scripts/EntityChangeDetector.cs"
"Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs"
"Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs"
"Assets/_Project/Scripts/Environment/WeatherEvents.cs"
"Assets/_Project/Scripts/Environment/WeatherProfile.cs"
"Assets/_Project/Scripts/EnvironmentState.cs"
"Assets/_Project/Scripts/EnvironmentalAnalyzerTool.cs"
"Assets/_Project/Scripts/FabricationAssemblerRuntime.cs"
"Assets/_Project/Scripts/FabricationRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Fabricator.cs"
"Assets/_Project/Scripts/FabricatorPhysicalActuator.cs"
"Assets/_Project/Scripts/FastCandidateMap.cs"
"Assets/_Project/Scripts/Fauna/ApexTerritoryProfile.cs"
"Assets/_Project/Scripts/Fauna/CreatureDamageManager.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.Foveated.cs"
"Assets/_Project/Scripts/Fauna/FaunaBrain.cs"
"Assets/_Project/Scripts/Fauna/FaunaDataTemplate.cs"
"Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs"
"Assets/_Project/Scripts/Fauna/FaunaLogicalLodTier.cs"
"Assets/_Project/Scripts/Fauna/FaunaPOI.cs"
"Assets/_Project/Scripts/Fauna/FaunaPresentationService.cs"
"Assets/_Project/Scripts/Fauna/FaunaScanRuntimeRegistry.cs"
"Assets/_Project/Scripts/Fauna/FaunaSensorSuite.cs"
"Assets/_Project/Scripts/Fauna/FaunaSimplifiedRagdollHandoff.cs"
"Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs"
"Assets/_Project/Scripts/Fauna/FaunaSpeciesProfile.cs"
"Assets/_Project/Scripts/Fauna/FaunaStateMachine.cs"
"Assets/_Project/Scripts/Fauna/FaunaSteeringEngine.cs"
"Assets/_Project/Scripts/Fauna/FaunaTentacleConstrainedIk.cs"
"Assets/_Project/Scripts/Fauna/FaunaTier1LodProxyRegistry.cs"
"Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs"
"Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs"
"Assets/_Project/Scripts/Fauna/MesofaunaFsmDebugGizmo.cs"
"Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs"
"Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs"
"Assets/_Project/Scripts/FaunaBiomeData.cs"
"Assets/_Project/Scripts/FaunaDirector.cs"
"Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/FieldLoadoutAdvisor.cs"
"Assets/_Project/Scripts/FieldOperationLogSystem.cs"
"Assets/_Project/Scripts/FieldTargetDescriptor.cs"
"Assets/_Project/Scripts/FieldTargetSemantics.cs"
"Assets/_Project/Scripts/FieldToolRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/FlashlightTool.cs"
"Assets/_Project/Scripts/FlowFieldProfile.cs"
"Assets/_Project/Scripts/FlowFieldVisualizer.cs"
"Assets/_Project/Scripts/FluidCompartmentTemplate.cs"
"Assets/_Project/Scripts/FluidIncursionSmokeTester.cs"
"Assets/_Project/Scripts/GameTickManager.cs"
"Assets/_Project/Scripts/Gameplay/BarterOfferCatalog.cs"
"Assets/_Project/Scripts/Gameplay/BarterOfferData.cs"
"Assets/_Project/Scripts/Gameplay/BaseAirlock.cs"
"Assets/_Project/Scripts/Gameplay/BaseAirlockEvents.cs"
"Assets/_Project/Scripts/Gameplay/BaseModuleCondensationSurface.cs"
"Assets/_Project/Scripts/Gameplay/BatteryCharger.cs"
"Assets/_Project/Scripts/Gameplay/BeaconRegistry.cs"
"Assets/_Project/Scripts/Gameplay/BioReactor.cs"
"Assets/_Project/Scripts/Gameplay/CelestialCataclysmSystem.cs"
"Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs"
"Assets/_Project/Scripts/Gameplay/Combat/BallisticsEditorFacade.cs"
"Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs"
"Assets/_Project/Scripts/Gameplay/ConsumableItem.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkMath.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs"
"Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs"
"Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs"
"Assets/_Project/Scripts/Gameplay/DebrisManager.cs"
"Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs"
"Assets/_Project/Scripts/Gameplay/DeployableFlare.cs"
"Assets/_Project/Scripts/Gameplay/DirectorMissionBridge.cs"
"Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs"
"Assets/_Project/Scripts/Gameplay/EndingSystem.cs"
"Assets/_Project/Scripts/Gameplay/EndingTerminalInteractable.cs"
"Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs"
"Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs"
"Assets/_Project/Scripts/Gameplay/Floater.cs"
"Assets/_Project/Scripts/Gameplay/FloraProjectile.cs"
"Assets/_Project/Scripts/Gameplay/GravTrap.cs"
"Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs"
"Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs"
"Assets/_Project/Scripts/Gameplay/HarvestablePlant.cs"
"Assets/_Project/Scripts/Gameplay/HazardExposureNotifier.cs"
"Assets/_Project/Scripts/Gameplay/HazardMutationProfile.cs"
"Assets/_Project/Scripts/Gameplay/HazardType.cs"
"Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs"
"Assets/_Project/Scripts/Gameplay/HazardZoneProfile.cs"
"Assets/_Project/Scripts/Gameplay/HeavyTowWinch.cs"
"Assets/_Project/Scripts/Gameplay/HectonCameraState.cs"
"Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs"
"Assets/_Project/Scripts/Gameplay/HectonHazardSource.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs"
"Assets/_Project/Scripts/Gameplay/HectonPlayerStateMachine.cs"
"Assets/_Project/Scripts/Gameplay/HectonScanRenderRegistry.cs"
"Assets/_Project/Scripts/Gameplay/HectonScannedRenderTarget.cs"
"Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs"
"Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs"
"Assets/_Project/Scripts/Gameplay/HostileFlora.cs"
"Assets/_Project/Scripts/Gameplay/IEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/IHectonPlayerEnvironmentHandler.cs"
"Assets/_Project/Scripts/Gameplay/IHectonPlayerStateMachine.cs"
"Assets/_Project/Scripts/Gameplay/IKinematicVehicleTransportSource.cs"
"Assets/_Project/Scripts/Gameplay/IMotorForces.cs"
"Assets/_Project/Scripts/Gameplay/IPlayerTransportLifecycleOwner.cs"
"Assets/_Project/Scripts/Gameplay/IPlayerTransportSource.cs"
"Assets/_Project/Scripts/Gameplay/ISubmarineRuntimeContext.cs"
"Assets/_Project/Scripts/Gameplay/ITowSnapReceiver.cs"
"Assets/_Project/Scripts/Gameplay/ITransportPlatform.cs"
"Assets/_Project/Scripts/Gameplay/ItemHighlight.cs"
"Assets/_Project/Scripts/Gameplay/LifePodDamageSystem.cs"
"Assets/_Project/Scripts/Gameplay/LifePodFireExtinguisherNozzle.cs"
"Assets/_Project/Scripts/Gameplay/LifePodTactilePrologueController.cs"
"Assets/_Project/Scripts/Gameplay/MantaEmergencyWreck.cs"
"Assets/_Project/Scripts/Gameplay/MantaScooter.cs"
"Assets/_Project/Scripts/Gameplay/MessageTerminal.cs"
"Assets/_Project/Scripts/Gameplay/MeteorSplashQuadVfx.cs"
"Assets/_Project/Scripts/Gameplay/MissionData.cs"
"Assets/_Project/Scripts/Gameplay/MissionManager.cs"
"Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs"
"Assets/_Project/Scripts/Gameplay/OxygenBubble.cs"
"Assets/_Project/Scripts/Gameplay/OxygenPlant.cs"
"Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs"
"Assets/_Project/Scripts/Gameplay/PlayerActionController.cs"
"Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs"
"Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs"
"Assets/_Project/Scripts/Gameplay/PlayerExpressionProfile.cs"
"Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/PlayerMovementBrineRuntimeSystem.cs"
"Assets/_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.Body.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimMotor.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs"
"Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerToolSwimContract.cs"
"Assets/_Project/Scripts/Gameplay/PlayerToolSwimHandedness.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportBinder.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportFeelContract.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportOccupancyMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportOrientationMode.cs"
"Assets/_Project/Scripts/Gameplay/PlayerTransportPreset.cs"
"Assets/_Project/Scripts/Gameplay/ProceduralFabrikArmJobs.cs"
"Assets/_Project/Scripts/Gameplay/RadiationHazard.cs"
"Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"
"Assets/_Project/Scripts/Gameplay/RandomEventMeteorMath.cs"
"Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs"
"Assets/_Project/Scripts/Gameplay/ResearchDirector.cs"
"Assets/_Project/Scripts/Gameplay/RuntimeSurvivalStats.cs"
"Assets/_Project/Scripts/Gameplay/SargassumCutResponder.cs"
"Assets/_Project/Scripts/Gameplay/SargassumMovementInfluence.cs"
"Assets/_Project/Scripts/Gameplay/SargassumPhysicsZone.cs"
"Assets/_Project/Scripts/Gameplay/ScannableFragment.cs"
"Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs"
"Assets/_Project/Scripts/Gameplay/SealedDoor.cs"
"Assets/_Project/Scripts/Gameplay/SolarPanel.cs"
"Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs"
"Assets/_Project/Scripts/Gameplay/SomaticSurvivalMath.cs"
"Assets/_Project/Scripts/Gameplay/StorageCrate.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineCompoundColliderAuthoring.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineProfile.cs"
"Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs"
"Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs"
"Assets/_Project/Scripts/Gameplay/SuitUpgradeResolver.cs"
"Assets/_Project/Scripts/Gameplay/SurvivalPhysiologyScalarJob.cs"
"Assets/_Project/Scripts/Gameplay/SurvivalStatusMasks.cs"
"Assets/_Project/Scripts/Gameplay/SwimPresentationProfile.cs"
"Assets/_Project/Scripts/Gameplay/SwimPresentationProfileLibrary.cs"
"Assets/_Project/Scripts/Gameplay/ToolEffectEvents.cs"
"Assets/_Project/Scripts/Gameplay/ToxinHazard.cs"
"Assets/_Project/Scripts/Gameplay/TransportChargingStation.cs"
"Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs"
"Assets/_Project/Scripts/Gameplay/VRSomaticRuntimeBootstrap.cs"
"Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs"
"Assets/_Project/Scripts/Gameplay/VehicleMotor.cs"
"Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs"
"Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs"
"Assets/_Project/Scripts/GlobalPhysicsStateManager.cs"
"Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs"
"Assets/_Project/Scripts/GravityTetherTool.cs"
"Assets/_Project/Scripts/HUDNotification.cs"
"Assets/_Project/Scripts/HUDQuickBar.cs"
"Assets/_Project/Scripts/HarpoonLauncherTool.cs"
"Assets/_Project/Scripts/HectonAtmosphereManager.cs"
"Assets/_Project/Scripts/HectonBiomeFamilyProfile.cs"
"Assets/_Project/Scripts/HectonBiomeLandmarkPlanProfile.cs"
"Assets/_Project/Scripts/HectonBiomeMatrixCatalog.cs"
"Assets/_Project/Scripts/HectonBiomeMatrixProfile.cs"
"Assets/_Project/Scripts/HectonBiomePlayProfile.cs"
"Assets/_Project/Scripts/HectonBiomeProfile.cs"
"Assets/_Project/Scripts/HectonBiomeRegistry.cs"
"Assets/_Project/Scripts/HectonBiomeResourceChannelProfile.cs"
"Assets/_Project/Scripts/HectonBiomeResourcePlanProfile.cs"
"Assets/_Project/Scripts/HectonBiomeSpatialPatternProfile.cs"
"Assets/_Project/Scripts/HectonBoidController.cs"
"Assets/_Project/Scripts/HectonCelestialEngine.cs"
"Assets/_Project/Scripts/HectonContactJob.cs"
"Assets/_Project/Scripts/HectonCrestOceanKinematics.cs"
"Assets/_Project/Scripts/HectonDirectorAI.cs"
"Assets/_Project/Scripts/HectonDiscoveryManager.cs"
"Assets/_Project/Scripts/HectonFabricatorUI.cs"
"Assets/_Project/Scripts/HectonFaunaFamilyProfile.cs"
"Assets/_Project/Scripts/HectonFloatingOrigin.cs"
"Assets/_Project/Scripts/HectonFluidEngine.cs"
"Assets/_Project/Scripts/HectonInventoryUI.cs"
"Assets/_Project/Scripts/HectonItem.cs"
"Assets/_Project/Scripts/HectonNarrativeDirector.cs"
"Assets/_Project/Scripts/HectonOceanPalette.cs"
"Assets/_Project/Scripts/HectonOceanRegistry.cs"
"Assets/_Project/Scripts/HectonPlayerMovement.cs"
"Assets/_Project/Scripts/HectonPlayerSpawner.cs"
"Assets/_Project/Scripts/HectonRockManager.cs"
"Assets/_Project/Scripts/HectonScanMarkerSystem.cs"
"Assets/_Project/Scripts/HectonSocketHelper.cs"
"Assets/_Project/Scripts/HectonSuitHUDExtensions.cs"
"Assets/_Project/Scripts/HectonSuitHUD_v4.cs"
"Assets/_Project/Scripts/HectonSurvivalSystem.cs"
"Assets/_Project/Scripts/HectonUnderwaterVisuals.cs"
"Assets/_Project/Scripts/HectonVoxelEngine.cs"
"Assets/_Project/Scripts/HectonVoxelVolume.cs"
"Assets/_Project/Scripts/HectonWorldGenerator.cs"
"Assets/_Project/Scripts/HydrationScheduler.cs"
"Assets/_Project/Scripts/IBuildPlacementRule.cs"
"Assets/_Project/Scripts/ICuttable.cs"
"Assets/_Project/Scripts/IFabricator.cs"
"Assets/_Project/Scripts/IHectonOceanKinematics.cs"
"Assets/_Project/Scripts/IOceanKinematics.cs"
"Assets/_Project/Scripts/IOriginShiftListener.cs"
"Assets/_Project/Scripts/IPoolable.cs"
"Assets/_Project/Scripts/IPowerComponent.cs"
"Assets/_Project/Scripts/ISaveable.cs"
"Assets/_Project/Scripts/ITickable.cs"
"Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs"
"Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs"
"Assets/_Project/Scripts/Interaction/HeavyCarryInteractable.cs"
"Assets/_Project/Scripts/Interaction/IInteractable.cs"
"Assets/_Project/Scripts/Interaction/IKinematicRepairTarget.cs"
"Assets/_Project/Scripts/Interaction/InteractableRegistry.cs"
"Assets/_Project/Scripts/Interaction/InteractionEvents.cs"
"Assets/_Project/Scripts/Interaction/InteractionUI.cs"
"Assets/_Project/Scripts/Interaction/InventoryPickupContracts.cs"
"Assets/_Project/Scripts/Interaction/KinematicTerminalInteractionBridge.cs"
"Assets/_Project/Scripts/Interaction/LifePodSeatStrapCoordinator.cs"
"Assets/_Project/Scripts/Interaction/LifePodSeatStrapLatch.cs"
"Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandController.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandReceiverRegistry.cs"
"Assets/_Project/Scripts/Interaction/PhysicalHandSide.cs"
"Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs"
"Assets/_Project/Scripts/Interaction/PhysicalSnapSwitch.cs"
"Assets/_Project/Scripts/Interaction/PhysicalToolGripOffsets.cs"
"Assets/_Project/Scripts/Interaction/PlayerInteraction.cs"
"Assets/_Project/Scripts/Interaction/SaveStation.cs"
"Assets/_Project/Scripts/Interaction/SuitDamageEvents.cs"
"Assets/_Project/Scripts/Interaction/VRCableDragPlug.cs"
"Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs"
"Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs"
"Assets/_Project/Scripts/InteractionHighlighter.cs"
"Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs"
"Assets/_Project/Scripts/Inventory/ItemPhysicalMetadata.cs"
"Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs"
"Assets/_Project/Scripts/Inventory/PressurizedContainer.cs"
"Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs"
"Assets/_Project/Scripts/InventoryEvents.cs"
"Assets/_Project/Scripts/InventoryGrid.cs"
"Assets/_Project/Scripts/ItemCatalog.cs"
"Assets/_Project/Scripts/ItemData.cs"
"Assets/_Project/Scripts/Items/PickupItem.cs"
"Assets/_Project/Scripts/KnifeTool.cs"
"Assets/_Project/Scripts/LandingImpactVFX.cs"
"Assets/_Project/Scripts/LaserCutter.cs"
"Assets/_Project/Scripts/LightDetectionSystem.cs"
"Assets/_Project/Scripts/LocKeys.Generated.cs"
"Assets/_Project/Scripts/LocNumericBuffer.cs"
"Assets/_Project/Scripts/LocRegistry.cs"
"Assets/_Project/Scripts/LocalizationEvents.cs"
"Assets/_Project/Scripts/LocalizationKeys.cs"
"Assets/_Project/Scripts/LocalizationManager.cs"
"Assets/_Project/Scripts/LocalizedAudioClipSet.cs"
"Assets/_Project/Scripts/LocalizedInlineIconResolver.cs"
"Assets/_Project/Scripts/LocalizedMeasurementFormatter.cs"
"Assets/_Project/Scripts/LocalizedSpriteRenderer.cs"
"Assets/_Project/Scripts/LocalizedTextReference.cs"
"Assets/_Project/Scripts/LocalizedWorldSign.cs"
"Assets/_Project/Scripts/LogicSpannerTool.cs"
"Assets/_Project/Scripts/MainMenuController.cs"
"Assets/_Project/Scripts/MainMenuInputRoutingGuard.cs"
"Assets/_Project/Scripts/MapMagicBridge.cs"
"Assets/_Project/Scripts/Meta/DifficultyModifierData.cs"
"Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs"
"Assets/_Project/Scripts/Meta/GlobalProfileData.cs"
"Assets/_Project/Scripts/Meta/GlobalProfileManager.cs"
"Assets/_Project/Scripts/Meta/MetaBuffInjector.cs"
"Assets/_Project/Scripts/Meta/MetaProfileUtility.cs"
"Assets/_Project/Scripts/Meta/MetaRuntimeInstaller.cs"
"Assets/_Project/Scripts/Meta/MetaUpgradeRegistry.cs"
"Assets/_Project/Scripts/Meta/RunModifierController.cs"
"Assets/_Project/Scripts/ModalWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/Editor/ModApiSandboxTunerWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/Editor/ModKernelInspectorWindow.cs"
"Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs"
"Assets/_Project/Scripts/ModdingAPI/HectonGameEvents.cs"
"Assets/_Project/Scripts/ModdingAPI/IHectonMod.cs"
"Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs"
"Assets/_Project/Scripts/ModdingAPI/IllegalContractException.cs"
"Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs"
"Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs"
"Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs"
"Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs"
"Assets/_Project/Scripts/ModdingAPI/ModLoader.cs"
"Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuModEntryView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuSettingSliderView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuSettingToggleView.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs"
"Assets/_Project/Scripts/ModdingAPI/ModMetadata.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRuntimeInfo.cs"
"Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs"
"Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs"
"Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs"
"Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs"
"Assets/_Project/Scripts/ModularEquipmentEngine.cs"
"Assets/_Project/Scripts/ModuleCatalog.cs"
"Assets/_Project/Scripts/ModuleMarker.cs"
"Assets/_Project/Scripts/ModuleSocket.cs"
"Assets/_Project/Scripts/ModuleStatusEvents.cs"
"Assets/_Project/Scripts/Narrative/ColonistLoreRegistry.cs"
"Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs"
"Assets/_Project/Scripts/Narrative/DeepReachCorporationData.cs"
"Assets/_Project/Scripts/Narrative/FaunaLoreRegistry.cs"
"Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs"
"Assets/_Project/Scripts/Narrative/LoreEncyclopediaLazyProxy.cs"
"Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs"
"Assets/_Project/Scripts/Narrative/NarrativeRuntimeInstaller.cs"
"Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs"
"Assets/_Project/Scripts/NarrativeDiscovery.cs"
"Assets/_Project/Scripts/NarrativeEvents.cs"
"Assets/_Project/Scripts/Networking/HectonNetworkManager.cs"
"Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs"
"Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs"
"Assets/_Project/Scripts/NoiseSystem.cs"
"Assets/_Project/Scripts/ObjectPoolDiagnostics.cs"
"Assets/_Project/Scripts/ObjectPoolManager.cs"
"Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs"
"Assets/_Project/Scripts/OmegaSurvivalKinematicsSmokeTester.cs"
"Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs"
"Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs"
"Assets/_Project/Scripts/Optimization/AssetRecord.cs"
"Assets/_Project/Scripts/Optimization/CameraRTManager.cs"
"Assets/_Project/Scripts/Optimization/GeneratedAssetGuidIdTable.cs"
"Assets/_Project/Scripts/Optimization/HardwareProfiler.cs"
"Assets/_Project/Scripts/Optimization/PostFXRTManager.cs"
"Assets/_Project/Scripts/Optimization/PreInitAssetIdMap.cs"
"Assets/_Project/Scripts/Optimization/RenderTextureAllocationRecord.cs"
"Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs"
"Assets/_Project/Scripts/Optimization/RenderTexturePool.cs"
"Assets/_Project/Scripts/Optimization/UIRTManager.cs"
"Assets/_Project/Scripts/Optimization/VRAMBudgetThresholds.cs"
"Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs"
"Assets/_Project/Scripts/Optimization/VRAMMonitor.cs"
"Assets/_Project/Scripts/Optimization/VRAMOptimizationBootstrap.cs"
"Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs"
"Assets/_Project/Scripts/Optimization/VisorRTManager.cs"
"Assets/_Project/Scripts/OriginShiftEventData.cs"
"Assets/_Project/Scripts/PDA/PDALogbookManager.cs"
"Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs"
"Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs"
"Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs"
"Assets/_Project/Scripts/PDA/PDAUtility.cs"
"Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs"
"Assets/_Project/Scripts/PDAInventoryTab.cs"
"Assets/_Project/Scripts/PerformanceMonitor.cs"
"Assets/_Project/Scripts/PersistentIDConverter.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementContracts.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/GlobalPhysicsStateManager.BuoyancyBridge.cs"
"Assets/_Project/Scripts/Physics/Buoyancy/PhysicsApplySystem.BuoyancyQueue.cs"
"Assets/_Project/Scripts/Physics/CablePhysicsDebugGizmo132.cs"
"Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs"
"Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationContracts.cs"
"Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs"
"Assets/_Project/Scripts/Physics/Editor/HabitatFluidIncursionTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Exosuit/Editor/ExosuitKinematicsTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs"
"Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs"
"Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs"
"Assets/_Project/Scripts/Physics/FluidMathCore.cs"
"Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs"
"Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.WakeRequests.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionCsv.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs"
"Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs"
"Assets/_Project/Scripts/Physics/KCC/Editor/HydrodynamicKccTunerWindow.cs"
"Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs"
"Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs"
"Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs"
"Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs"
"Assets/_Project/Scripts/Physics/TetherBlackBoxDumpWriter.cs"
"Assets/_Project/Scripts/Physics/TetherSignals.cs"
"Assets/_Project/Scripts/Physics/TetherVerletJobs.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/Editor/SubmarineAutopilotTunerWindow.cs"
"Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs"
"Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs"
"Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs"
"Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs"
"Assets/_Project/Scripts/Physics/VerletCableDTOs.cs"
"Assets/_Project/Scripts/PhysicsApplySystem.cs"
"Assets/_Project/Scripts/PlacementGhost.cs"
"Assets/_Project/Scripts/PlayerBuilder.cs"
"Assets/_Project/Scripts/PlayerFlashlight.cs"
"Assets/_Project/Scripts/PlayerFootstepAudio.cs"
"Assets/_Project/Scripts/PlayerInventory.cs"
"Assets/_Project/Scripts/PlayerLocomotionMode.cs"
"Assets/_Project/Scripts/PlayerPDA.cs"
"Assets/_Project/Scripts/PlayerThrusterAudio.cs"
"Assets/_Project/Scripts/PlayerTool.cs"
"Assets/_Project/Scripts/PlayerToolManager.cs"
"Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs"
"Assets/_Project/Scripts/Power/PowerGridModuleData.cs"
"Assets/_Project/Scripts/Power/PowerGridTelemetryEvents.cs"
"Assets/_Project/Scripts/Power/PowerRelayNode.cs"
"Assets/_Project/Scripts/Power/ReactorCoreProfile.cs"
"Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs"
"Assets/_Project/Scripts/Power/SubmarineOsThermalGridGizmo.cs"
"Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs"
"Assets/_Project/Scripts/Power/WfcOutpostGridRegistry.cs"
"Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs"
"Assets/_Project/Scripts/PowerGrid.cs"
"Assets/_Project/Scripts/PowerGridManager.cs"
"Assets/_Project/Scripts/PowerNode.cs"
"Assets/_Project/Scripts/PrefabRegistry.cs"
"Assets/_Project/Scripts/ProceduralFamily_Fauna.cs"
"Assets/_Project/Scripts/ProfilerRegistry.cs"
"Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs"
"Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs"
"Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs"
"Assets/_Project/Scripts/Progression/ProgressionRuntimeInstaller.cs"
"Assets/_Project/Scripts/PropulsionTool.cs"
"Assets/_Project/Scripts/ProximityColliderSystem.cs"
"Assets/_Project/Scripts/QueryCacheContext.cs"
"Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs"
"Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs"
"Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs"
"Assets/_Project/Scripts/Quest/QuestDagMockSignalJobs.cs"
"Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs"
"Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs"
"Assets/_Project/Scripts/Quest/QuestData.cs"
"Assets/_Project/Scripts/Quest/QuestEvents.cs"
"Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs"
"Assets/_Project/Scripts/Quest/QuestManager.cs"
"Assets/_Project/Scripts/Quest/QuestRuntimeTypes.cs"
"Assets/_Project/Scripts/Quest/QuestStateManager.cs"
"Assets/_Project/Scripts/RTLProcessor.cs"
"Assets/_Project/Scripts/RaycastBatchHelper.cs"
"Assets/_Project/Scripts/RecipeData.cs"
"Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs"
"Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs"
"Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs"
"Assets/_Project/Scripts/Rendering/LutArrayResolver.cs"
"Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs"
"Assets/_Project/Scripts/RepairTool.cs"
"Assets/_Project/Scripts/ResourceNode.cs"
"Assets/_Project/Scripts/RockAttachmentData.cs"
"Assets/_Project/Scripts/RockDataLink.cs"
"Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs"
"Assets/_Project/Scripts/RuntimeInstanceId.cs"
"Assets/_Project/Scripts/RuntimePerformanceProfiler.cs"
"Assets/_Project/Scripts/SalvageSamplerTool.cs"
"Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs"
"Assets/_Project/Scripts/SaveBinaryStorage.cs"
"Assets/_Project/Scripts/SaveBinaryStorageNativeArrayExtensions.cs"
"Assets/_Project/Scripts/SaveData.cs"
"Assets/_Project/Scripts/SaveDataMigration.cs"
"Assets/_Project/Scripts/SaveDataMigration_AupV8.cs"
"Assets/_Project/Scripts/SaveEvents.cs"
"Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs"
"Assets/_Project/Scripts/SaveManager.cs"
"Assets/_Project/Scripts/SaveMetadata.cs"
"Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs"
"Assets/_Project/Scripts/SaveRecoverySmokeTester.cs"
"Assets/_Project/Scripts/SaveSidecarStorage.cs"
"Assets/_Project/Scripts/SaveSlotAuditResult.cs"
"Assets/_Project/Scripts/SaveSlotInfo.cs"
"Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs"
"Assets/_Project/Scripts/SaveSlotRepairResult.cs"
"Assets/_Project/Scripts/SaveSlotUI.cs"
"Assets/_Project/Scripts/SaveSystem/Editor/EntitySaveTunerWindow.cs"
"Assets/_Project/Scripts/SaveSystem/Editor/VoxelSaveTunerWindow.cs"
"Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs"
"Assets/_Project/Scripts/SaveSystem/EntityDeltaGizmoProbe.cs"
"Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs"
"Assets/_Project/Scripts/SaveSystem/H8WalInspector.cs"
"Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs"
"Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs"
"Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs"
"Assets/_Project/Scripts/SaveSystem/SteamCloudSaveConflictResolver.cs"
"Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs"
"Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs"
"Assets/_Project/Scripts/SaveThumbnailSystem.cs"
"Assets/_Project/Scripts/ScanEvents.cs"
"Assets/_Project/Scripts/ScanLogSystem.cs"
"Assets/_Project/Scripts/ScanRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/ScannableCategoryUtility.cs"
"Assets/_Project/Scripts/ScannableTarget.cs"
"Assets/_Project/Scripts/ScannerTool.cs"
"Assets/_Project/Scripts/ScatterBudgetController.cs"
"Assets/_Project/Scripts/ScavengePopulator.cs"
"Assets/_Project/Scripts/Scavenging/HarvestableTemplate.cs"
"Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs"
"Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs"
"Assets/_Project/Scripts/SeamGapDitherRenderer.cs"
"Assets/_Project/Scripts/SeamRegistry.cs"
"Assets/_Project/Scripts/SkySystemFollowCamera.cs"
"Assets/_Project/Scripts/SpatialAudioManager.cs"
"Assets/_Project/Scripts/StringBuilderPool.cs"
"Assets/_Project/Scripts/StunPistolTool.cs"
"Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs"
"Assets/_Project/Scripts/SubmarineElectrolysisModule.cs"
"Assets/_Project/Scripts/SubmarineFluidDynamics.cs"
"Assets/_Project/Scripts/SubmarineStructuralGrid.cs"
"Assets/_Project/Scripts/SuitData.cs"
"Assets/_Project/Scripts/SuitHUDProfile.cs"
"Assets/_Project/Scripts/SurfaceStateUtility.cs"
"Assets/_Project/Scripts/SurvivalKinematicsSmokeTester.cs"
"Assets/_Project/Scripts/SurvivalStats.cs"
"Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs"
"Assets/_Project/Scripts/TetherClass.cs"
"Assets/_Project/Scripts/TetherInstance.cs"
"Assets/_Project/Scripts/TetherManager.cs"
"Assets/_Project/Scripts/TetherProfileSO.cs"
"Assets/_Project/Scripts/ThermalGeyser.cs"
"Assets/_Project/Scripts/ThermalMeltSmokeTester.cs"
"Assets/_Project/Scripts/ThermalSurvivalSmokeTester.cs"
"Assets/_Project/Scripts/ThermalUpdraftVolume.cs"
"Assets/_Project/Scripts/ThreatCostTable.cs"
"Assets/_Project/Scripts/ToolHitUtility.cs"
"Assets/_Project/Scripts/ToolLoadoutProvisioner.cs"
"Assets/_Project/Scripts/ToolRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/ToolStagingSpawner.cs"
"Assets/_Project/Scripts/ToolTrialRangeRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/Tools/EquipmentHardwareSpecsCsvParser.cs"
"Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs"
"Assets/_Project/Scripts/Tools/HapticWaveformLibrary.cs"
"Assets/_Project/Scripts/Tools/IBatteryTool.cs"
"Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs"
"Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs"
"Assets/_Project/Scripts/Tools/PerformanceMonitor.cs"
"Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs"
"Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs"
"Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs"
"Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs"
"Assets/_Project/Scripts/Tools/ToolLoadoutPreset.cs"
"Assets/_Project/Scripts/Tools/ToolMetadata.cs"
"Assets/_Project/Scripts/Tools/ToolModuleData.cs"
"Assets/_Project/Scripts/Tools/ToolUpgradeData.cs"
"Assets/_Project/Scripts/Tools/ToolUpgradeSystem.cs"
"Assets/_Project/Scripts/Tools/VerificationRuntimeProbe.cs"
"Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs"
"Assets/_Project/Scripts/UI/ARWaypointOverlay.cs"
"Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs"
"Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs"
"Assets/_Project/Scripts/UI/ActionProgressHUD.cs"
"Assets/_Project/Scripts/UI/AnalogGaugeNeedle3D.cs"
"Assets/_Project/Scripts/UI/AudioWaveformAnimator.cs"
"Assets/_Project/Scripts/UI/BIOSMessageStreamer.cs"
"Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs"
"Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs"
"Assets/_Project/Scripts/UI/BeaconHUDElement.cs"
"Assets/_Project/Scripts/UI/BlackBoxMetricDashboard.cs"
"Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs"
"Assets/_Project/Scripts/UI/CharBufferPool.cs"
"Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs"
"Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs"
"Assets/_Project/Scripts/UI/DiegeticHudTextNode.cs"
"Assets/_Project/Scripts/UI/DiegeticPDAController.cs"
"Assets/_Project/Scripts/UI/DiegeticPanelController.cs"
"Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs"
"Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs"
"Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs"
"Assets/_Project/Scripts/UI/EngineHealthOverlay.cs"
"Assets/_Project/Scripts/UI/FakeRadarBlipController.cs"
"Assets/_Project/Scripts/UI/FontAssetRecovery.cs"
"Assets/_Project/Scripts/UI/FontStreamingManager.cs"
"Assets/_Project/Scripts/UI/GhostSignalUtility.cs"
"Assets/_Project/Scripts/UI/GlitchEncoder.cs"
"Assets/_Project/Scripts/UI/GlitchTable.cs"
"Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs"
"Assets/_Project/Scripts/UI/HectonOSBootManager.cs"
"Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs"
"Assets/_Project/Scripts/UI/HectonTextNode.cs"
"Assets/_Project/Scripts/UI/HectonUIScaler.cs"
"Assets/_Project/Scripts/UI/HphiReactiveUiTelemetry.cs"
"Assets/_Project/Scripts/UI/HudNumericStringCache.cs"
"Assets/_Project/Scripts/UI/InteractionUI.cs"
"Assets/_Project/Scripts/UI/LabelSwapScheduler.cs"
"Assets/_Project/Scripts/UI/LoadingScreenController.cs"
"Assets/_Project/Scripts/UI/LoadingTipsDisplay.cs"
"Assets/_Project/Scripts/UI/LocOverflowHandler.cs"
"Assets/_Project/Scripts/UI/LocalizedFontResolver.cs"
"Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs"
"Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs"
"Assets/_Project/Scripts/UI/LocalizedTextMadnessFx.cs"
"Assets/_Project/Scripts/UI/MainMenuAudioIntegration.cs"
"Assets/_Project/Scripts/UI/NotificationEvents.cs"
"Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs"
"Assets/_Project/Scripts/UI/PDABarterTab.cs"
"Assets/_Project/Scripts/UI/PDAConstructionTab.cs"
"Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs"
"Assets/_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs"
"Assets/_Project/Scripts/UI/PDADataLogTab.cs"
"Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs"
"Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs"
"Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs"
"Assets/_Project/Scripts/UI/PDAIntrusionManager.cs"
"Assets/_Project/Scripts/UI/PDAInventoryFilterButton.cs"
"Assets/_Project/Scripts/UI/PDALoadoutTab.cs"
"Assets/_Project/Scripts/UI/PDAMapTab.cs"
"Assets/_Project/Scripts/UI/PDAShellChrome.cs"
"Assets/_Project/Scripts/UI/PDASpectrumTab.cs"
"Assets/_Project/Scripts/UI/PDATabButton.cs"
"Assets/_Project/Scripts/UI/PauseControlsPanel.cs"
"Assets/_Project/Scripts/UI/PauseMenuAudioIntegration.cs"
"Assets/_Project/Scripts/UI/PauseMenuController.cs"
"Assets/_Project/Scripts/UI/PauseMenuHost.cs"
"Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs"
"Assets/_Project/Scripts/UI/PhysicalPanelButton.cs"
"Assets/_Project/Scripts/UI/PhysicalPanelDial.cs"
"Assets/_Project/Scripts/UI/PhysicalTerminalKeyboard.cs"
"Assets/_Project/Scripts/UI/RelayHUDElement.cs"
"Assets/_Project/Scripts/UI/RelayHUDRuntimeBootstrap.cs"
"Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs"
"Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs"
"Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs"
"Assets/_Project/Scripts/UI/SettingsComparisonView.cs"
"Assets/_Project/Scripts/UI/SettingsLivePreview.cs"
"Assets/_Project/Scripts/UI/SettingsManager.cs"
"Assets/_Project/Scripts/UI/SettingsPanel.cs"
"Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs"
"Assets/_Project/Scripts/UI/SettingsPanelProfiler.cs"
"Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs"
"Assets/_Project/Scripts/UI/SonarHoloCompass.cs"
"Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs"
"Assets/_Project/Scripts/UI/SubnauticaSystemsDebugUI.cs"
"Assets/_Project/Scripts/UI/SubtitleManager.cs"
"Assets/_Project/Scripts/UI/SuitAdvisoryController.cs"
"Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs"
"Assets/_Project/Scripts/UI/SurvivalHUDController.cs"
"Assets/_Project/Scripts/UI/TMP_TextRegistry.cs"
"Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs"
"Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs"
"Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs"
"Assets/_Project/Scripts/UI/UIAudioFeedback.cs"
"Assets/_Project/Scripts/UI/UIButtonAudioTrigger.cs"
"Assets/_Project/Scripts/UI/UIFadeTransition.cs"
"Assets/_Project/Scripts/UI/UIParticleEffect.cs"
"Assets/_Project/Scripts/UI/UIScreenShake.cs"
"Assets/_Project/Scripts/UI/UISliderValueDisplay.cs"
"Assets/_Project/Scripts/UI/UITooltip.cs"
"Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs"
"Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs"
"Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs"
"Assets/_Project/Scripts/UIRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/VFX/BiomeProfile.cs"
"Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs"
"Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs"
"Assets/_Project/Scripts/VFX/NativeTrailRenderer.cs"
"Assets/_Project/Scripts/VFX/ShakeProfile.cs"
"Assets/_Project/Scripts/VFX/VFXEmissionProfile.cs"
"Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs"
"Assets/_Project/Scripts/VFX/VolumetricFogContracts.cs"
"Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs"
"Assets/_Project/Scripts/VFX/Wakes/WakeDisplacementData.cs"
"Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs"
"Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs"
"Assets/_Project/Scripts/Visor/DeferredDecalPass.cs"
"Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs"
"Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs"
"Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs"
"Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs"
"Assets/_Project/Scripts/Visor/Editor/ScreenSpaceDecalTunerWindow.cs"
"Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs"
"Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs"
"Assets/_Project/Scripts/Visor/HectonBiosDiagnosticState.cs"
"Assets/_Project/Scripts/Visor/HectonDrsRenderFeatureGate.cs"
"Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs"
"Assets/_Project/Scripts/Visor/HectonDryVolumeStencilSource.cs"
"Assets/_Project/Scripts/Visor/HectonFillrateDepthPrepassFeature.cs"
"Assets/_Project/Scripts/Visor/HectonFlashlightVoxelShadowProvider.cs"
"Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs"
"Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs"
"Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs"
"Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs"
"Assets/_Project/Scripts/Visor/HectonOverdrawHeatmapFeature.cs"
"Assets/_Project/Scripts/Visor/HectonRetinaDistortionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs"
"Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs"
"Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVRDiegeticFocusController.cs"
"Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs"
"Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs"
"Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs"
"Assets/_Project/Scripts/Visor/PlayerStressVFX.cs"
"Assets/_Project/Scripts/Visor/SonarGridOverlay.cs"
"Assets/_Project/Scripts/Visor/SpectrumSystem.cs"
"Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs"
"Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs"
"Assets/_Project/Scripts/Visor/VisorHUDController.cs"
"Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs"
"Assets/_Project/Scripts/VisualBudgetSmokeTester.cs"
"Assets/_Project/Scripts/VisualCascadeSmokeTester.cs"
"Assets/_Project/Scripts/VisualOmegaSmokeTester.cs"
"Assets/_Project/Scripts/VortexVolume.cs"
"Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs"
"Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs"
"Assets/_Project/Scripts/VoxelDeltaPersistenceDTO.cs"
"Assets/_Project/Scripts/VoxelDeltaProcessor.cs"
"Assets/_Project/Scripts/VoxelRuntimeIntegrityUtility.cs"
"Assets/_Project/Scripts/VoxelSeamDirector.cs"
"Assets/_Project/Scripts/World/AUPMath.cs"
"Assets/_Project/Scripts/World/AbsoluteUniversePositionBlit.cs"
"Assets/_Project/Scripts/World/AbyssalFluidDecalManager.cs"
"Assets/_Project/Scripts/World/AbyssalThermalManager.cs"
"Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs"
"Assets/_Project/Scripts/World/BasePollutionManager.cs"
"Assets/_Project/Scripts/World/BioCableIK.cs"
"Assets/_Project/Scripts/World/Biolum/CaveBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/FloorBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs"
"Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs"
"Assets/_Project/Scripts/World/Biolum/OceanBiolumZone.cs"
"Assets/_Project/Scripts/World/BiomeMatrixSmokeTester.cs"
"Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs"
"Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfJobs.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntimeBootstrap.cs"
"Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs"
"Assets/_Project/Scripts/World/Biomes/Editor/BiomeTransitionTunerWindow.cs"
"Assets/_Project/Scripts/World/BoidStructValidator.cs"
"Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs"
"Assets/_Project/Scripts/World/ChunkLocalOffsetQuantization.cs"
"Assets/_Project/Scripts/World/CrestDepthCacheDebugger.cs"
"Assets/_Project/Scripts/World/CrestFoamDebugger.cs"
"Assets/_Project/Scripts/World/CullingManager.cs"
"Assets/_Project/Scripts/World/DepthZoneDirector.cs"
"Assets/_Project/Scripts/World/DepthZoneProfile.cs"
"Assets/_Project/Scripts/World/DestructibleOrganicManager.cs"
"Assets/_Project/Scripts/World/DispatcherJobSwap.cs"
"Assets/_Project/Scripts/World/DropBuffer.cs"
"Assets/_Project/Scripts/World/DynamicResolutionScaler.cs"
"Assets/_Project/Scripts/World/EcosystemBalanceProfile.cs"
"Assets/_Project/Scripts/World/EcosystemDirector.cs"
"Assets/_Project/Scripts/World/EcosystemEnvelope.cs"
"Assets/_Project/Scripts/World/Editor/AbyssalScentTunerWindow.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelay.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs"
"Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs"
"Assets/_Project/Scripts/World/EntropyYieldJob.cs"
"Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs"
"Assets/_Project/Scripts/World/ErosionHarnessJobs.cs"
"Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs"
"Assets/_Project/Scripts/World/FloraBrain.cs"
"Assets/_Project/Scripts/World/FloraDataTemplate.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeCsvHotloader.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs"
"Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs"
"Assets/_Project/Scripts/World/FloraInteractionManager.cs"
"Assets/_Project/Scripts/World/FloraRegrowthDirector.cs"
"Assets/_Project/Scripts/World/GPR/GroundRadarJobs.cs"
"Assets/_Project/Scripts/World/GPUScatterDirector.cs"
"Assets/_Project/Scripts/World/GeneticTraitProfile.cs"
"Assets/_Project/Scripts/World/GlobalWorldSampler.cs"
"Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs"
"Assets/_Project/Scripts/World/HLODInstance.cs"
"Assets/_Project/Scripts/World/HectonAnomalyBrineJobs.cs"
"Assets/_Project/Scripts/World/HectonAnomalyEngine.cs"
"Assets/_Project/Scripts/World/HectonAnomalyFeatureJobs.cs"
"Assets/_Project/Scripts/World/HectonAnomalyResourceBinding.cs"
"Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs"
"Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs"
"Assets/_Project/Scripts/World/HectonBiolumController.cs"
"Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs"
"Assets/_Project/Scripts/World/HectonBrineToxicMudGrid.cs"
"Assets/_Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs"
"Assets/_Project/Scripts/World/HectonCaveVoxelLightingVolume.cs"
"Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs"
"Assets/_Project/Scripts/World/HectonHLODRenderer.cs"
"Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs"
"Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs"
"Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs"
"Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorData.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs"
"Assets/_Project/Scripts/World/HectonOctahedralImpostorTypes.cs"
"Assets/_Project/Scripts/World/HectonProceduralVegetationStripBuilder.cs"
"Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs"
"Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs"
"Assets/_Project/Scripts/World/HectonSpatialHash.cs"
"Assets/_Project/Scripts/World/HectonVegetationConstants.cs"
"Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs"
"Assets/_Project/Scripts/World/HectonWorldStreamingTypes.cs"
"Assets/_Project/Scripts/World/HydraulicErosionJob.cs"
"Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs"
"Assets/_Project/Scripts/World/ISargassumMassiveDisplacementReceiver.cs"
"Assets/_Project/Scripts/World/ImpostorSystem.cs"
"Assets/_Project/Scripts/World/InstancedFloraRenderer.cs"
"Assets/_Project/Scripts/World/LODSystemManager.cs"
"Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"
"Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs"
"Assets/_Project/Scripts/World/ProceduralFamily_Fauna.cs"
"Assets/_Project/Scripts/World/ProceduralFamily_Flora.cs"
"Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs"
"Assets/_Project/Scripts/World/ProxyLightRegistry.cs"
"Assets/_Project/Scripts/World/ResourceDistributionDirector.cs"
"Assets/_Project/Scripts/World/ResourceYieldMath.cs"
"Assets/_Project/Scripts/World/SamplingSnapshot.cs"
"Assets/_Project/Scripts/World/SargassumCollapseChunk.cs"
"Assets/_Project/Scripts/World/SargassumCrestDampingController.cs"
"Assets/_Project/Scripts/World/SargassumCutManager.cs"
"Assets/_Project/Scripts/World/SargassumDebrisParticleSystem.cs"
"Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs"
"Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs"
"Assets/_Project/Scripts/World/ScatterBackendBindingBridge.cs"
"Assets/_Project/Scripts/World/ScatterBackendBindingState.cs"
"Assets/_Project/Scripts/World/ScatterBackendParityReference.cs"
"Assets/_Project/Scripts/World/ScatterBackendRequestFactory.cs"
"Assets/_Project/Scripts/World/ScatterBackendRuntimeHost.cs"
"Assets/_Project/Scripts/World/ScatterBackendRuntimeStatus.cs"
"Assets/_Project/Scripts/World/ScatterBackendScheduleRequest.cs"
"Assets/_Project/Scripts/World/ScatterBackendShadowCompletion.cs"
"Assets/_Project/Scripts/World/ScatterBackendSupportContext.cs"
"Assets/_Project/Scripts/World/ScatterCandidateEvaluator.cs"
"Assets/_Project/Scripts/World/ScatterClassicBackendAdapters.cs"
"Assets/_Project/Scripts/World/ScatterDiagnosticsTracker.cs"
"Assets/_Project/Scripts/World/ScatterEvaluationEngine.cs"
"Assets/_Project/Scripts/World/ScatterEvaluator.cs"
"Assets/_Project/Scripts/World/ScatterGPUIBackend.cs"
"Assets/_Project/Scripts/World/ScatterHeuristicsUtility.cs"
"Assets/_Project/Scripts/World/ScatterHybridRuntimeEntryPoint.cs"
"Assets/_Project/Scripts/World/ScatterInstancingService.cs"
"Assets/_Project/Scripts/World/ScatterMath.cs"
"Assets/_Project/Scripts/World/ScatterRebuildProfileSnapshot.cs"
"Assets/_Project/Scripts/World/ScatterReconcileMetrics.cs"
"Assets/_Project/Scripts/World/ScatterRuntimeBackendFacade.cs"
"Assets/_Project/Scripts/World/SedimentAccumulationManager.cs"
"Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs"
"Assets/_Project/Scripts/World/SoundscapeSystem.cs"
"Assets/_Project/Scripts/World/SpatialSonarSnapshot.cs"
"Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs"
"Assets/_Project/Scripts/World/TectonicActivityProfile.cs"
"Assets/_Project/Scripts/World/ThermalSlumpingJob.cs"
"Assets/_Project/Scripts/World/VegetationCapacityUtilities.cs"
"Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs"
"Assets/_Project/Scripts/World/VegetationDensityQueryService.cs"
"Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs"
"Assets/_Project/Scripts/World/VegetationMath.cs"
"Assets/_Project/Scripts/World/VegetationMemoryPool.cs"
"Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs"
"Assets/_Project/Scripts/World/VegetationPersistenceManager.cs"
"Assets/_Project/Scripts/World/VegetationPredatorFearField.cs"
"Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs"
"Assets/_Project/Scripts/World/VegetationThermalSampler.cs"
"Assets/_Project/Scripts/World/VegetationThreatAndStructureService.cs"
"Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs"
"Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs"
"Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs"
"Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs"
"Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntimeLifecycle.cs"
"Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs"
"Assets/_Project/Scripts/World/WorldGenRegistrySmokeTester.cs"
"Assets/_Project/Scripts/World/WorldGenerativeGeologyTelemetry.cs"
"Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs"
"Assets/_Project/Scripts/World/WorldPickupStateCodec.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainFakeOverhangJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainTectonicDisplacementJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainTerraceJobs.cs"
"Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs"
"Assets/_Project/Scripts/World/WorldReadabilityDirector.cs"
"Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs"
"Assets/_Project/Scripts/World/WorldShippingContentFilter.cs"
"Assets/_Project/Scripts/World/WorldShippingSceneRuntimeGuard.cs"
"Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs"
"Assets/_Project/Scripts/World/WorldVolumetricBiomeClassificationJobs.cs"
"Assets/_Project/Scripts/World/WreckMaterialRegistry.cs"
"Assets/_Project/Scripts/WorldCaveDirector.cs"
"Assets/_Project/Scripts/WorldChunkCoordinate.cs"
"Assets/_Project/Scripts/WorldChunkStreamingProfile.cs"
"Assets/_Project/Scripts/WorldContentDirector.cs"
"Assets/_Project/Scripts/WorldContentProfile.cs"
"Assets/_Project/Scripts/WorldContentSocket.cs"
"Assets/_Project/Scripts/WorldExpeditionLoopProfile.cs"
"Assets/_Project/Scripts/WorldFaunaSpawnRegistry.cs"
"Assets/_Project/Scripts/WorldFidelityRoot.cs"
"Assets/_Project/Scripts/WorldGeneratedPrimitiveFactory.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyMeshBuilder.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyProfile.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyRuntimeSmokeTester.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologySeamPlan.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyService.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBlendRequest.cs"
"Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs"
"Assets/_Project/Scripts/WorldInterestAnchor.cs"
"Assets/_Project/Scripts/WorldInterestDirector.cs"
"Assets/_Project/Scripts/WorldMacroZoneCoordinate.cs"
"Assets/_Project/Scripts/WorldMotivationProfile.cs"
"Assets/_Project/Scripts/WorldPopulationDirector.cs"
"Assets/_Project/Scripts/WorldPopulationRule.cs"
"Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs"
"Assets/_Project/Scripts/WorldProceduralBiomeFamilyContextCatalog.cs"
"Assets/_Project/Scripts/WorldProceduralBiomeFamilyContextProfile.cs"
"Assets/_Project/Scripts/WorldProceduralClusterFocus.cs"
"Assets/_Project/Scripts/WorldProceduralFaunaMood.cs"
"Assets/_Project/Scripts/WorldProceduralFieldSampler.cs"
"Assets/_Project/Scripts/WorldProceduralFillDirector.cs"
"Assets/_Project/Scripts/WorldProceduralPattern.cs"
"Assets/_Project/Scripts/WorldProceduralPatternCatalog.cs"
"Assets/_Project/Scripts/WorldProceduralPatternProfile.cs"
"Assets/_Project/Scripts/WorldProceduralPlaceholderMarker.cs"
"Assets/_Project/Scripts/WorldProceduralPlacementRule.cs"
"Assets/_Project/Scripts/WorldProceduralProxyInstance.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirector.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorDiagnosticsContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorEnvironmentalEnvelope.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorPlacementRetentionContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorPlacementTypes.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorReconcileContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorRescueContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorRuntimeStateContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSpatialHelpers.cs"
"Assets/_Project/Scripts/WorldProceduralScatterDirectorSpawnBatchContexts.cs"
"Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs"
"Assets/_Project/Scripts/WorldProceduralStateRegistry.cs"
"Assets/_Project/Scripts/WorldProceduralStructureFocus.cs"
"Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs"
"Assets/_Project/Scripts/WorldSandboxAttractionProfile.cs"
"Assets/_Project/Scripts/WorldSliceAnchor.cs"
"Assets/_Project/Scripts/WorldSliceDirector.cs"
"Assets/_Project/Scripts/WorldStateManager.cs"
"Assets/_Project/Scripts/WorldStreamingDirector.cs"
"Assets/_Project/Scripts/WorldStreamingLayer.cs"
"Assets/_Project/Scripts/WorldZoneAnchor.cs"
"Assets/_Project/Scripts/WorldZoneDirector.cs"
"Assets/_Project/Scripts/WorldZonePlanProfile.cs"
"Assets/_Project/Scripts/WorldZoneProfile.cs"
"Assets/_Project/Scripts/ZeroGCStringCache.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
[3121/3439 10s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.World.ProceduralWreckage.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageContracts.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs"
"Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
[3122/3439 10s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.World.ProceduralCoral.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Burst.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralContracts.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralGpuUploadDispatcher.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs"
"Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs"
-langversion:9.0
/unsafe+
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
[3123/3439 10s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll (+2 others)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp2"
Contents of Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Narrative.Prologue.rsp
-target:library
-out:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll"
-refout:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.ref.dll"
-define:UNITY_6000_4_1
-define:UNITY_6000_4
-define:UNITY_6000
-define:UNITY_5_3_OR_NEWER
-define:UNITY_5_4_OR_NEWER
-define:UNITY_5_5_OR_NEWER
-define:UNITY_5_6_OR_NEWER
-define:UNITY_2017_1_OR_NEWER
-define:UNITY_2017_2_OR_NEWER
-define:UNITY_2017_3_OR_NEWER
-define:UNITY_2017_4_OR_NEWER
-define:UNITY_2018_1_OR_NEWER
-define:UNITY_2018_2_OR_NEWER
-define:UNITY_2018_3_OR_NEWER
-define:UNITY_2018_4_OR_NEWER
-define:UNITY_2019_1_OR_NEWER
-define:UNITY_2019_2_OR_NEWER
-define:UNITY_2019_3_OR_NEWER
-define:UNITY_2019_4_OR_NEWER
-define:UNITY_2020_1_OR_NEWER
-define:UNITY_2020_2_OR_NEWER
-define:UNITY_2020_3_OR_NEWER
-define:UNITY_2021_1_OR_NEWER
-define:UNITY_2021_2_OR_NEWER
-define:UNITY_2021_3_OR_NEWER
-define:UNITY_2022_1_OR_NEWER
-define:UNITY_2022_2_OR_NEWER
-define:UNITY_2022_3_OR_NEWER
-define:UNITY_2023_1_OR_NEWER
-define:UNITY_2023_2_OR_NEWER
-define:UNITY_2023_3_OR_NEWER
-define:UNITY_6000_0_OR_NEWER
-define:UNITY_6000_1_OR_NEWER
-define:UNITY_6000_2_OR_NEWER
-define:UNITY_6000_3_OR_NEWER
-define:UNITY_6000_4_OR_NEWER
-define:PLATFORM_ARCH_64
-define:UNITY_64
-define:UNITY_INCLUDE_TESTS
-define:ENABLE_AR
-define:ENABLE_AUDIO
-define:ENABLE_AUDIO_SCRIPTABLE_PIPELINE
-define:ENABLE_CACHING
-define:ENABLE_CLOTH
-define:ENABLE_EVENT_QUEUE
-define:ENABLE_MICROPHONE
-define:ENABLE_MULTIPLE_DISPLAYS
-define:ENABLE_PHYSICS
-define:ENABLE_TEXTURE_STREAMING
-define:ENABLE_VIRTUALTEXTURING
-define:ENABLE_LZMA
-define:ENABLE_UNITYEVENTS
-define:ENABLE_VR
-define:ENABLE_WEBCAM
-define:ENABLE_UNITYWEBREQUEST
-define:ENABLE_WWW
-define:ENABLE_CLOUD_SERVICES
-define:ENABLE_CLOUD_SERVICES_ADS
-define:ENABLE_CLOUD_SERVICES_USE_WEBREQUEST
-define:ENABLE_UNITY_CONSENT
-define:ENABLE_UNITY_CLOUD_IDENTIFIERS
-define:ENABLE_CLOUD_SERVICES_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING
-define:ENABLE_CLOUD_SERVICES_PURCHASING
-define:ENABLE_CLOUD_SERVICES_ANALYTICS
-define:ENABLE_CLOUD_SERVICES_BUILD
-define:ENABLE_EDITOR_GAME_SERVICES
-define:ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
-define:ENABLE_CLOUD_LICENSE
-define:ENABLE_EDITOR_HUB_LICENSE
-define:ENABLE_WEBSOCKET_CLIENT
-define:ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
-define:ENABLE_DIRECTOR_AUDIO
-define:ENABLE_DIRECTOR_TEXTURE
-define:ENABLE_MANAGED_JOBS
-define:ENABLE_MANAGED_TRANSFORM_JOBS
-define:ENABLE_MANAGED_ANIMATION_JOBS
-define:ENABLE_MANAGED_AUDIO_JOBS
-define:ENABLE_MANAGED_UNITYTLS
-define:INCLUDE_DYNAMIC_GI
-define:ENABLE_SCRIPTING_GC_WBARRIERS
-define:PLATFORM_SUPPORTS_MONO
-define:RENDER_SOFTWARE_CURSOR
-define:ENABLE_MARSHALLING_TESTS
-define:ENABLE_VIDEO
-define:ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK
-define:ENABLE_ACCELERATOR_CLIENT_DEBUGGING
-define:ENABLE_ACCESSIBILITY_SCREEN_READER
-define:TEXTCORE_1_0_OR_NEWER
-define:EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED
-define:PLATFORM_STANDALONE_WIN
-define:PLATFORM_STANDALONE
-define:UNITY_STANDALONE_WIN
-define:UNITY_STANDALONE
-define:ENABLE_RUNTIME_GI
-define:ENABLE_MOVIES
-define:ENABLE_NETWORK
-define:ENABLE_NVIDIA
-define:ENABLE_AMD
-define:ENABLE_CRUNCH_TEXTURE_COMPRESSION
-define:ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS
-define:ENABLE_OUT_OF_PROCESS_CRASH_HANDLER
-define:ENABLE_CLUSTER_SYNC
-define:ENABLE_CLUSTERINPUT
-define:PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP
-define:GFXDEVICE_WAITFOREVENT_MESSAGEPUMP
-define:PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER
-define:PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION
-define:PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS
-define:ENABLE_MONO
-define:NET_STANDARD_2_0
-define:NET_STANDARD
-define:NET_STANDARD_2_1
-define:NETSTANDARD
-define:NETSTANDARD2_1
-define:ENABLE_PROFILER
-define:ENABLE_PROFILER_ASSISTANT_INTEGRATION
-define:DEBUG
-define:TRACE
-define:UNITY_ASSERTIONS
-define:UNITY_EDITOR
-define:UNITY_EDITOR_64
-define:UNITY_EDITOR_WIN
-define:ENABLE_UNITY_COLLECTIONS_CHECKS
-define:ENABLE_BURST_AOT
-define:UNITY_TEAM_LICENSE
-define:ENABLE_CUSTOM_RENDER_TEXTURE
-define:ENABLE_DIRECTOR
-define:ENABLE_LOCALIZATION
-define:ENABLE_SPRITES
-define:ENABLE_TERRAIN
-define:ENABLE_TILEMAP
-define:ENABLE_TIMELINE
-define:ENABLE_INPUT_SYSTEM
-define:TEXTCORE_FONT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
-define:TEXTCORE_FONT_ENGINE_1_6_OR_NEWER
-define:DOTWEEN
-define:CREST_OCEAN
-define:CREST_URP
-define:__MICROSPLAT__
-define:MAPMAGIC2
-define:MM_NATIVE
-define:UNITY_VISUAL_SCRIPTING
-define:GPU_INSTANCER
-define:ODIN_INSPECTOR
-define:ODIN_INSPECTOR_3
-define:ODIN_INSPECTOR_3_1
-define:AMPLIFY_SHADER_EDITOR
-define:SHAPES_URP
-define:MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED
-define:BAKERY_INCLUDED
-define:VLB_URP
-define:ODIN_INSPECTOR_3_2
-define:ODIN_INSPECTOR_3_3
-define:CSHARP_7_OR_LATER
-define:CSHARP_7_3_OR_NEWER
-r:"Assets/AstarPathfindingProject/Plugins/Clipper/Pathfinding.ClipperLib.dll"
-r:"Assets/AstarPathfindingProject/Plugins/DotNetZip/Pathfinding.Ionic.Zip.Reduced.dll"
-r:"Assets/AstarPathfindingProject/Plugins/Poly2Tri/Pathfinding.Poly2Tri.dll"
-r:"Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Plugins/Mono.Data.Sqlite.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerEditorLib.dll"
-r:"Assets/MeshBaker/Libs/MeshBakerLib.dll"
-r:"Assets/Plugins/Demigiant/DOTween/DOTween.dll"
-r:"Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll"
-r:"Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenProEditor.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/DemiLib.dll"
-r:"Assets/Plugins/Demigiant/DemiLib/Core/Editor/DemiEditor.dll"
-r:"Assets/Plugins/Editor/RelationsInspector/RelationsInspector.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.CSharp.dll"
-r:"Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll"
-r:"Assets/Plugins/Roslyn/System.Collections.Immutable.dll"
-r:"Assets/Plugins/Roslyn/System.Reflection.Metadata.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Reflection.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.Editor.dll"
-r:"Assets/Plugins/Sirenix/Assemblies/Sirenix.Utilities.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEditor.Graphs.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/Unity.Scripting.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AdaptivePerformanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.AssetComplianceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.BuildProfileModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreBusinessMetricsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.DiagnosticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EditorToolbarModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.EmbreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphToolkitModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridAndSnapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.HierarchyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MediaModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PlayModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PresetsUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ProjectAuditorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickInstallModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.QuickSearchModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SafeModeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneTemplateModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SceneViewModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderBuildSettingsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderCompilationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.ShaderFoundryModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SketchUpModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.TreeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIAutomationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIBuilderModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIElementsSamplesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UIToolkitAuthoringModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEditor.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ARModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AccessibilityModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AndroidJNIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AnimationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClothModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterInputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ClusterRendererModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.CrashReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DSPGraphModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.DirectorModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GameCenterModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GraphicsStateCollectionSerializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HierarchyCoreModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.HotReloadModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IMGUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.IdentifiersModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputForUIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.InsightsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.LocalizationModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MarshallingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.MultiplayerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ParticleSystemModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PerformanceReportingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsBackendPhysXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.PropertiesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RenderAs2DModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScreenCaptureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ScriptingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.ShaderVariantAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteMaskModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SpriteShapeModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.StreamingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubstanceModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.SubsystemsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TLSModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TerrainPhysicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextCoreTextEngineModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UmbraModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsCommonModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityAnalyticsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConnectModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityConsentModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityCurlModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAssetBundleModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestAudioModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestTextureModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestWWWModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VFXModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VectorGraphicsModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VehiclesModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.VirtualTexturingModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.WindModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.XRModule.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed/UnityEngine/UnityEngine.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/Extensions/2.0.0/System.Runtime.InteropServices.WindowsRuntime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ComponentModel.Composition.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Core.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Data.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Drawing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.IO.Compression.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Net.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Runtime.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.ServiceModel.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Transactions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Web.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Windows.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.Serialization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/System.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/Microsoft.Win32.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.AppContext.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Buffers.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Concurrent.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.NonGeneric.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.Specialized.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Collections.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.EventBasedAsync.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.TypeConverter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ComponentModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Console.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Data.Common.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Contracts.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Debug.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.FileVersionInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Process.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.StackTrace.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TextWriterTraceListener.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tools.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.TraceSource.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Diagnostics.Tracing.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Drawing.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Dynamic.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Calendars.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Globalization.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.ZipFile.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Compression.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.DriveInfo.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.Watcher.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.FileSystem.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.IsolatedStorage.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.MemoryMappedFiles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.Pipes.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.UnmanagedMemoryStream.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.IO.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Expressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.Queryable.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Linq.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Http.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NameResolution.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.NetworkInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Ping.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Requests.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Security.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.Sockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebHeaderCollection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.Client.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Net.WebSockets.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Numerics.Vectors.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ObjectModel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.DispatchProxy.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.ILGeneration.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.Lightweight.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Emit.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Reflection.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Reader.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.ResourceManager.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Resources.Writer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.CompilerServices.VisualC.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Handles.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.RuntimeInformation.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.InteropServices.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Numerics.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Formatters.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Json.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.Serialization.Xml.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Claims.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Algorithms.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Csp.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.Primitives.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Cryptography.X509Certificates.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.Principal.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Security.SecureString.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.Encoding.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Text.RegularExpressions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Overlapped.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.Parallel.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Tasks.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Thread.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.ThreadPool.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.Timer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Threading.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.ValueTuple.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.ReaderWriter.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.XDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XPath.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlDocument.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Xml.XmlSerializer.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"
-r:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Antlr3.Runtime.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/Unity.Plastic.Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/log4netPlastic.dll"
-r:"Library/PackageCache/com.unity.collab-proxy@a5329f833fa8/Lib/Editor/unityplastic.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll"
-r:"Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections.Tests/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll"
-r:"Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll"
-r:"Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll"
-r:"Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll"
-r:"Library/PackageCache/com.unity.sharp-zip-lib@f6e4ef34e4d8/Runtime/Unity.SharpZipLib.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/DotNetZip/Unity.VisualScripting.IonicZip.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Editor/VisualScripting.Core/EditorAssetResources/Unity.VisualScripting.TextureAssets.dll"
-r:"Library/PackageCache/com.unity.visualscripting@8bed5ad90189/Runtime/VisualScripting.Flow/Dependencies/NCalc/Unity.VisualScripting.Antlr3.Runtime.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Contracts.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Collections.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/Unity.Mathematics.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEditor.UI.ref.dll"
-r:"Library/Bee/artifacts/1900b0aEDbg.dag/UnityEngine.UI.ref.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll"
-analyzer:"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/Unity.UIToolkit.SourceGenerator.dll"
"Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs"
-langversion:9.0
/deterministic
/optimize-
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.UnityAdditionalFile.txt"
Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
ExitCode
1
Output
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
[3124/3439 10s] ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.MockDomain.Runtime.dll (+pdb)
CommandLine
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\Compilation\Unity.ILPP.Trigger\Unity.ILPP.Trigger.exe" @"Library\Bee\artifacts\rsp\12719471298722492838.rsp"
Contents of Library\Bee\artifacts\rsp\12719471298722492838.rsp
"unity-ilpp-7964abe555f4ec2c1439bad844e00f5c" p "Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll" "Library/Bee/artifacts/1900b0aEDbg.dag/post-processed" "UNITY_6000_4_1" "UNITY_6000_4" "UNITY_6000" "UNITY_5_3_OR_NEWER" "UNITY_5_4_OR_NEWER" "UNITY_5_5_OR_NEWER" "UNITY_5_6_OR_NEWER" "UNITY_2017_1_OR_NEWER" "UNITY_2017_2_OR_NEWER" "UNITY_2017_3_OR_NEWER" "UNITY_2017_4_OR_NEWER" "UNITY_2018_1_OR_NEWER" "UNITY_2018_2_OR_NEWER" "UNITY_2018_3_OR_NEWER" "UNITY_2018_4_OR_NEWER" "UNITY_2019_1_OR_NEWER" "UNITY_2019_2_OR_NEWER" "UNITY_2019_3_OR_NEWER" "UNITY_2019_4_OR_NEWER" "UNITY_2020_1_OR_NEWER" "UNITY_2020_2_OR_NEWER" "UNITY_2020_3_OR_NEWER" "UNITY_2021_1_OR_NEWER" "UNITY_2021_2_OR_NEWER" "UNITY_2021_3_OR_NEWER" "UNITY_2022_1_OR_NEWER" "UNITY_2022_2_OR_NEWER" "UNITY_2022_3_OR_NEWER" "UNITY_2023_1_OR_NEWER" "UNITY_2023_2_OR_NEWER" "UNITY_2023_3_OR_NEWER" "UNITY_6000_0_OR_NEWER" "UNITY_6000_1_OR_NEWER" "UNITY_6000_2_OR_NEWER" "UNITY_6000_3_OR_NEWER" "UNITY_6000_4_OR_NEWER" "PLATFORM_ARCH_64" "UNITY_64" "UNITY_INCLUDE_TESTS" "ENABLE_AR" "ENABLE_AUDIO" "ENABLE_AUDIO_SCRIPTABLE_PIPELINE" "ENABLE_CACHING" "ENABLE_CLOTH" "ENABLE_EVENT_QUEUE" "ENABLE_MICROPHONE" "ENABLE_MULTIPLE_DISPLAYS" "ENABLE_PHYSICS" "ENABLE_TEXTURE_STREAMING" "ENABLE_VIRTUALTEXTURING" "ENABLE_LZMA" "ENABLE_UNITYEVENTS" "ENABLE_VR" "ENABLE_WEBCAM" "ENABLE_UNITYWEBREQUEST" "ENABLE_WWW" "ENABLE_CLOUD_SERVICES" "ENABLE_CLOUD_SERVICES_ADS" "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST" "ENABLE_UNITY_CONSENT" "ENABLE_UNITY_CLOUD_IDENTIFIERS" "ENABLE_CLOUD_SERVICES_CRASH_REPORTING" "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING" "ENABLE_CLOUD_SERVICES_PURCHASING" "ENABLE_CLOUD_SERVICES_ANALYTICS" "ENABLE_CLOUD_SERVICES_BUILD" "ENABLE_EDITOR_GAME_SERVICES" "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT" "ENABLE_CLOUD_LICENSE" "ENABLE_EDITOR_HUB_LICENSE" "ENABLE_WEBSOCKET_CLIENT" "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API" "ENABLE_DIRECTOR_AUDIO" "ENABLE_DIRECTOR_TEXTURE" "ENABLE_MANAGED_JOBS" "ENABLE_MANAGED_TRANSFORM_JOBS" "ENABLE_MANAGED_ANIMATION_JOBS" "ENABLE_MANAGED_AUDIO_JOBS" "ENABLE_MANAGED_UNITYTLS" "INCLUDE_DYNAMIC_GI" "ENABLE_SCRIPTING_GC_WBARRIERS" "PLATFORM_SUPPORTS_MONO" "RENDER_SOFTWARE_CURSOR" "ENABLE_MARSHALLING_TESTS" "ENABLE_VIDEO" "ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK" "ENABLE_ACCELERATOR_CLIENT_DEBUGGING" "ENABLE_ACCESSIBILITY_SCREEN_READER" "TEXTCORE_1_0_OR_NEWER" "EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED" "PLATFORM_STANDALONE_WIN" "PLATFORM_STANDALONE" "UNITY_STANDALONE_WIN" "UNITY_STANDALONE" "ENABLE_RUNTIME_GI" "ENABLE_MOVIES" "ENABLE_NETWORK" "ENABLE_NVIDIA" "ENABLE_AMD" "ENABLE_CRUNCH_TEXTURE_COMPRESSION" "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS" "ENABLE_OUT_OF_PROCESS_CRASH_HANDLER" "ENABLE_CLUSTER_SYNC" "ENABLE_CLUSTERINPUT" "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP" "GFXDEVICE_WAITFOREVENT_MESSAGEPUMP" "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER" "PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION" "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS" "ENABLE_MONO" "NET_STANDARD_2_0" "NET_STANDARD" "NET_STANDARD_2_1" "NETSTANDARD" "NETSTANDARD2_1" "ENABLE_PROFILER" "ENABLE_PROFILER_ASSISTANT_INTEGRATION" "DEBUG" "TRACE" "UNITY_ASSERTIONS" "UNITY_EDITOR" "UNITY_EDITOR_64" "UNITY_EDITOR_WIN" "ENABLE_UNITY_COLLECTIONS_CHECKS" "ENABLE_BURST_AOT" "UNITY_TEAM_LICENSE" "ENABLE_CUSTOM_RENDER_TEXTURE" "ENABLE_DIRECTOR" "ENABLE_LOCALIZATION" "ENABLE_SPRITES" "ENABLE_TERRAIN" "ENABLE_TILEMAP" "ENABLE_TIMELINE" "ENABLE_INPUT_SYSTEM" "TEXTCORE_FONT_ENGINE_1_5_OR_NEWER" "TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER" "TEXTCORE_FONT_ENGINE_1_6_OR_NEWER" "DOTWEEN" "CREST_OCEAN" "CREST_URP" "__MICROSPLAT__" "MAPMAGIC2" "MM_NATIVE" "UNITY_VISUAL_SCRIPTING" "GPU_INSTANCER" "ODIN_INSPECTOR" "ODIN_INSPECTOR_3" "ODIN_INSPECTOR_3_1" "AMPLIFY_SHADER_EDITOR" "SHAPES_URP" "MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED" "BAKERY_INCLUDED" "VLB_URP" "ODIN_INSPECTOR_3_2" "ODIN_INSPECTOR_3_3" "H8_BURST_FUNCTION_POINTERS" "CSHARP_7_OR_LATER" "CSHARP_7_3_OR_NEWER" -r "Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Global.Contracts.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.MockDomain.Contracts.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Unity.Burst.dll" "Library\Bee\artifacts\1900b0aEDbg.dag\Unity.Mathematics.dll" "Assets\AstarPathfindingProject\Plugins\Clipper\Pathfinding.ClipperLib.dll" "Assets\AstarPathfindingProject\Plugins\DotNetZip\Pathfinding.Ionic.Zip.Reduced.dll" "Assets\AstarPathfindingProject\Plugins\Poly2Tri\Pathfinding.Poly2Tri.dll" "Assets\Candice AI for Games\Scripts\Libs\Candice Save System\Plugins\Mono.Data.Sqlite.dll" "Assets\MeshBaker\Libs\MeshBakerEditorLib.dll" "Assets\MeshBaker\Libs\MeshBakerLib.dll" "Assets\Plugins\Demigiant\DOTween\DOTween.dll" "Assets\Plugins\Demigiant\DOTween\Editor\DOTweenEditor.dll" "Assets\Plugins\Demigiant\DOTweenPro\DOTweenPro.dll" "Assets\Plugins\Demigiant\DOTweenPro\Editor\DOTweenProEditor.dll" "Assets\Plugins\Demigiant\DemiLib\Core\DemiLib.dll" "Assets\Plugins\Demigiant\DemiLib\Core\Editor\DemiEditor.dll" "Assets\Plugins\Editor\RelationsInspector\RelationsInspector.dll" "Assets\Plugins\Roslyn\Microsoft.CodeAnalysis.CSharp.dll" "Assets\Plugins\Roslyn\Microsoft.CodeAnalysis.dll" "Assets\Plugins\Roslyn\System.Collections.Immutable.dll" "Assets\Plugins\Roslyn\System.Reflection.Metadata.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.OdinInspector.Attributes.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.OdinInspector.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Reflection.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Serialization.Config.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Serialization.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Utilities.Editor.dll" "Assets\Plugins\Sirenix\Assemblies\Sirenix.Utilities.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\Extensions\2.0.0\System.Runtime.InteropServices.WindowsRuntime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.ComponentModel.Composition.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Core.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Data.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Drawing.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.IO.Compression.FileSystem.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Net.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Numerics.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Runtime.Serialization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.ServiceModel.Web.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Transactions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Web.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Windows.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.Linq.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.Serialization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.Xml.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\System.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\Microsoft.Win32.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.AppContext.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Buffers.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.Concurrent.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.NonGeneric.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.Specialized.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Collections.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.EventBasedAsync.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.TypeConverter.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ComponentModel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Console.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Data.Common.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Contracts.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Debug.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.FileVersionInfo.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Process.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.StackTrace.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.TextWriterTraceListener.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Tools.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.TraceSource.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Diagnostics.Tracing.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Drawing.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Dynamic.Runtime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.Calendars.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Globalization.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Compression.ZipFile.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Compression.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.DriveInfo.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.Watcher.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.FileSystem.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.IsolatedStorage.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.MemoryMappedFiles.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.Pipes.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.UnmanagedMemoryStream.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.IO.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Expressions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Parallel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.Queryable.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Linq.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Memory.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Http.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.NameResolution.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.NetworkInformation.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Ping.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Requests.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Security.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.Sockets.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebHeaderCollection.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebSockets.Client.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Net.WebSockets.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Numerics.Vectors.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ObjectModel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.DispatchProxy.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.ILGeneration.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.Lightweight.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Emit.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Reflection.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.Reader.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.ResourceManager.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Resources.Writer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.CompilerServices.VisualC.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Handles.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.InteropServices.RuntimeInformation.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.InteropServices.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Numerics.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Formatters.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Json.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.Serialization.Xml.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Runtime.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Claims.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Algorithms.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Csp.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Encoding.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.Primitives.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Cryptography.X509Certificates.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.Principal.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Security.SecureString.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.Encoding.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.Encoding.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Text.RegularExpressions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Overlapped.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.Extensions.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.Parallel.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Tasks.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Thread.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.ThreadPool.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.Timer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Threading.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.ValueTuple.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.ReaderWriter.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XPath.XDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XPath.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XmlDocument.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\compat\2.1.0\shims\netstandard\System.Xml.XmlSerializer.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\Unity.Android.Gradle.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\AndroidPlayer\Unity.Android.Types.dll" "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\MacStandaloneSupport\UnityEditor.iOS.Extensions.Xcode.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\Unity.Plastic.Antlr3.Runtime.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\Unity.Plastic.Newtonsoft.Json.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\log4netPlastic.dll" "Library\PackageCache\com.unity.collab-proxy@a5329f833fa8\Lib\Editor\unityplastic.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.LowLevel.ILSupport\Unity.Collections.LowLevel.ILSupport.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.Tests\System.IO.Hashing\System.IO.Hashing.dll" "Library\PackageCache\com.unity.collections@538ace9075bc\Unity.Collections.Tests\System.Runtime.CompilerServices.Unsafe\System.Runtime.CompilerServices.Unsafe.dll" "Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll" "Library\PackageCache\com.unity.nuget.mono-cecil@ecb9724e46ff\Mono.Cecil.dll" "Library\PackageCache\com.unity.nuget.newtonsoft-json@4dfd81071c64\Runtime\Newtonsoft.Json.dll" "Library\PackageCache\com.unity.sharp-zip-lib@f6e4ef34e4d8\Runtime\Unity.SharpZipLib.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\Dependencies\DotNetZip\Unity.VisualScripting.IonicZip.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\Dependencies\YamlDotNet\Unity.VisualScripting.YamlDotNet.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Editor\VisualScripting.Core\EditorAssetResources\Unity.VisualScripting.TextureAssets.dll" "Library\PackageCache\com.unity.visualscripting@8bed5ad90189\Runtime\VisualScripting.Flow\Dependencies\NCalc\Unity.VisualScripting.Antlr3.Runtime.dll"
ExitCode
-1073740791
Output
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
*** Tundra build failed (12.46 seconds), 6 items updated, 3439 evaluated
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp2"
Output:
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralWreckage.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.World.ProceduralCoral.rsp2"
Output:
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Script Compilation Error for: Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.dll (+2 others)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp" "@Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp2"
Output:
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Script Compilation Error for: ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.MockDomain.Runtime.dll (+pdb)
CmdLine: "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\Compilation\Unity.ILPP.Trigger\Unity.ILPP.Trigger.exe" @"Library\Bee\artifacts\rsp\12719471298722492838.rsp"
Output:
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,18): error CS1513: expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,25): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,38): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(196,70): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,44): error CS1519: Invalid token '&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(197,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,13): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,26): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,37): error CS1519: Invalid token '&&' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,51): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(199,98): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,40): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(215,51): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,33): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,45): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,78): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1018: Keyword 'this' or 'base' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(216,82): error CS1519: Invalid token '0f' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,27): error CS1519: Invalid token ' =' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(217,60): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,27): error CS1519: Invalid token '>' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(218,74): error CS1519: Invalid token ')' in class, record, struct, or interface member declaration
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,50): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,58): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(219,65): error CS1001: Identifier expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,13): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS8124: Tuple must contain at least two elements.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,40): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,46): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,56): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,89): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(222,103): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,52): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(223,59): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,21): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,27): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1003: Syntax error, '(' expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1026: ) expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1002: ; expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,52): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,53): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(225,60): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,44): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,79): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,81): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,83): error CS0116: namespace cannot directly contain members such as fields, methods or statements
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(227,86): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS8803: Top-level statements must precede namespace and type declarations.
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(230,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(243,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(260,9): error CS0106: modifier 'private' is not valid for this item
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(268,5): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\Physics\HabitatFluidIncursionJobs.cs(735,1): error CS1022: Type or namespace definition, or end-of-file expected
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(583,42): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageVault.cs(1143,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralWreckage\ProceduralWreckageJobs.cs(705,50): error CS0117: 'float4x4' does not contain definition for 'Rotate'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralJobs.cs(312,53): error CS0121: call is ambiguous between following methods or properties: 'math.min(int, int)' and 'math.min(uint2, uint2)'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(464,56): warning CS0162: Unreachable code detected
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(563,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(571,17): error CS8332: Cannot assign to member of variable 'in ProceduralCoralVaultBuffers' because it is readonly variable
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(935,37): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\World\ProceduralCoral\ProceduralCoralVault.cs(1445,38): error CS0117: 'math' does not contain definition for 'reversebytes'
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(181,17): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,13): error CS0103: name 'NativeMemorySentinel' does not exist in current context
Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs(452,123): error CS0103: name 'NativeAllocationLifetime' does not exist in current context
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.MockDomain.Runtime.dll, with 158 defines and 168 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
zzzUnity.Burst.CodeGen.BurstILPostProcessor: ILPostProcessor has thrown exception: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
PostProcessing failed: System.InvalidOperationException: Internal compiler error for Burst ILPostProcessor on Hecton8.MockDomain.Runtime. Exception: System.NullReferenceException: Object reference not set to instance of object.
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform. b__28_0(CustomAttribute x)
at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.LocateFunctionPointerTCreation(MethodDefinition m, Instruction i)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokes(MethodDefinition m)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.CollectDelegateInvokesFromType(TypeDefinition type)
at zzzUnity.Burst.CodeGen.FunctionPointerInvokeTransform.Run(AssemblyDefinition assemblyDefinition)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at zzzUnity.Burst.CodeGen.BurstILPostProcessor.Process(ICompiledAssembly compiledAssembly)
at Unity.ILPP.Runner.PostProcessingPipeline.PostProcessAssemblyAsync(PostProcessAssemblyRequest request, Action`2 progressSink)
at Unity.ILPP.Runner.PostProcessingService.PostProcessAssembly(PostProcessAssemblyRequest request, IServerStreamWriter`1 responseStream, ServerCallContext context)
Unhandled Exception: System.InvalidOperationException: Post processing failed
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0xdc1
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at Unity.ILPP.Trigger.TriggerApp. d__1.MoveNext() + 0x347
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Unity.ILPP.Trigger.TriggerApp. d__0.MoveNext() + 0xcb
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. $>d__0.MoveNext() + 0x1a5
--- End of stack trace from previous location ---
at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
at Program. (String[] args) + 0x24
at Unity.ILPP.Trigger! +0x404bf3
AssetDatabase: script compilation time: 14.724358s
Scripts have compiler errors.
Exiting without bug reporter. Application will terminate with return code 1
===== END FILE: Unity_SHINOBU_160_compile_after_hotpath.log =====

===== FILE: Unity_SHINOBU_38_Run_final_exitprocess.log =====
[Licensing::Module] Trying to connect to existing licensing client channel...
Built from '6000.4/staging' branch; Version is '6000.4.1f1 (8535861f39e1) revision 8729990'; Using compiler version '194234433'; Build Type 'Release'
[Licensing::IpcConnector] Channel LicenseClient-danat doesn't exist
OS: 'Windows 11 (10.0.26200) CoreSingleLanguage' Language: 'en' Physical Memory: 32407 MB
BatchMode: 1, IsHumanControllingUs: 0, StartBugReporterOnCrash: 0, Is64bit: 1
System architecture: x64
Process architecture: x64
Date: 2026-05-18T08:18:17Z
[Licensing::Module] Successfully launched LicensingClient (PId: 32764)
COMMAND LINE ARGUMENTS:
C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe
-batchmode
-nographics
-projectPath
C:\hades\Hecton8
-executeMethod
Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run
-h8qa
-h8qaDistanceMeters
10000
-h8qaFastForward
100
-h8qaTelemetryHz
4
-logFile
Docs\AgentLogs\Unity_SHINOBU_38_Run_final_exitprocess.log
Successfully changed project path to: C:\hades\Hecton8
C:/hades/Hecton8
[UnityMemory] Configuration Parameters - Can be set up in boot.config
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-gi-baking-worker=262144"
"memorysetup-temp-allocator-size-nav-mesh-worker=65536"
"memorysetup-temp-allocator-size-audio-worker=65536"
"memorysetup-temp-allocator-size-cloud-worker=32768"
"memorysetup-temp-allocator-size-gfx=262144"
"memorysetup-temp-allocator-size-preload-manager=33554432"
"memorysetup-temp-allocator-size-job-worker=262144"
"memorysetup-temp-allocator-size-background-worker=32768"
"memorysetup-allocator-temp-initial-block-size-main=262144"
"memorysetup-allocator-temp-initial-block-size-worker=262144"
"memorysetup-bucket-allocator-granularity=16"
"memorysetup-bucket-allocator-bucket-count=8"
"memorysetup-bucket-allocator-block-size=33554432"
"memorysetup-bucket-allocator-block-count=8"
"memorysetup-main-allocator-block-size=16777216"
"memorysetup-thread-allocator-block-size=16777216"
"memorysetup-gfx-main-allocator-block-size=16777216"
"memorysetup-gfx-thread-allocator-block-size=16777216"
"memorysetup-cache-allocator-block-size=4194304"
"memorysetup-typetree-allocator-block-size=2097152"
"memorysetup-profiler-bucket-allocator-granularity=16"
"memorysetup-profiler-bucket-allocator-bucket-count=8"
"memorysetup-profiler-bucket-allocator-block-size=33554432"
"memorysetup-profiler-bucket-allocator-block-count=8"
"memorysetup-profiler-allocator-block-size=16777216"
"memorysetup-profiler-editor-allocator-block-size=1048576"
"memorysetup-temp-allocator-size-main=16777216"
"memorysetup-job-temp-allocator-block-size=2097152"
"memorysetup-job-temp-allocator-block-size-background=1048576"
"memorysetup-job-temp-allocator-reduction-small-platforms=262144"
Player connection [26236] Target information:
Player connection [26236] * "[IP] 192.168.1.130 [Port] 55504 [Flags] 2 [Guid] 1585693318 [EditorId] 1585693318 [Version] 1048832 [Id] WindowsEditor(7,Shinobu) [Debug] 1 [PackageName] WindowsEditor [ProjectName] Editor"
Player connection [26236] * "[IP] 172.18.0.1 [Port] 55504 [Flags] 2 [Guid] 1585693318 [EditorId] 1585693318 [Version] 1048832 [Id] WindowsEditor(7,Shinobu) [Debug] 1 [PackageName] WindowsEditor [ProjectName] Editor"
Player connection [26236] Host joined multi-casting on [225.0.0.222:54997]...
Player connection [26236] Host joined alternative multi-casting on [225.0.0.222:34997]...
Input System module state changed to: Initialized.
[Physics::Module] Initialized fallback backend.
[Physics::Module] Id: 0xdecafbad
[Licensing::IpcConnector] Successfully connected to: "LicenseClient-danat" at "2026-05-18T08:18:17.5047801Z"
[Package Manager] Connected to IPC stream "Upm-40220" after 0.4 seconds.
[Licensing::Module] Licensing is not yet initialized.
[Licensing::Client] Handshaking with LicensingClient:
Version: 1.18.1+9fbee8e
Session Id: 79e4def5243041088e6335cb8207d8d5
Correlation Id: 279e76a26771304512714267c6f8091f
External correlation Id: 4272298739436542770
Machine Id: KXBg4HkLZwVfPhjJrzyzSmUVWFw=
[Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-danat" (connect: 0.38s, validation: 0.07s, handshake: 0.90s)
[Licensing::IpcConnector] Successfully connected to: "LicenseClient-danat-notifications" at "2026-05-18T08:18:18.4729298Z"
[Licensing::Module] Connected to LicensingClient (PId: 32764, launch time: 0.00, total connection time: 1.35s)
[Licensing::Module] Error: Access token is unavailable; failed to update
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Module] License group:
Id: 7972536317136-UnityPersXXXX
Product: Unity Personal
Type: Assigned
Expiration: Unlimited
[Licensing::Client] Successfully updated license, isAsync: True, time: 0.01
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Module] Licensing Background thread has ended after 1.38s
[Licensing::Module] Licensing is initialized (took 0.63s).
[Licensing::Client] Successfully resolved entitlement details
Library Redirect Path: Library/
[Physics::Module] Selected backend.
[Physics::Module] Name: PhysX
[Physics::Module] Id: 0xf2b8ea05
[Physics::Module] SDK Version: 4.1.2
[Physics::Module] Integration Version: 1.0.0
[Physics::Module] Threading Mode: Multi-Threaded
Refreshing native plugins compatible for Editor in 204.12 ms, found 27 plugins.
Initialize engine version: 6000.4.1f1 (8535861f39e1)
[Subsystems] Discovering subsystems at path C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Resources/UnitySubsystems
[Subsystems] Discovering subsystems at path C:/hades/Hecton8/Assets
Forcing GfxDevice: Null
GfxDevice: creating device client; kGfxThreadingModeNonThreaded
NullGfxDevice:
Version: NULL 1.0 [1.0]
Renderer: Null Device
Vendor: Unity Technologies
Initialize mono
Mono path[0] = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/Managed'
Mono path[1] = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/MonoBleedingEdge/lib/mono/unityjit-win32'
Mono config path = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/MonoBleedingEdge/etc'
Using monoOptions --debugger-agent=transport=dt_socket,embedding=1,server=y,suspend=n,address=127.0.0.1:56220
CodeReloadManager initialized
Using cacheserver namespaces - metadata:defaultmetadata, artifacts:defaultartifacts
Using cacheserver namespaces - metadata:defaultmetadata, artifacts:defaultartifacts
ImportWorker Server TCP listen port: 0
AcceleratorClientConnectionCallback - disconnected - :0
Begin MonoManager ReloadAssembly
Registering precompiled unity dll's ...
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/UnityEditor.WindowsStandalone.Extensions.dll
[Licensing::Client] Successfully resolved entitlement details
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll
Register platform support module: C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport/UnityEditor.LinuxStandalone.Extensions.dll
Registered in 0.004308 seconds.
Native extension for LinuxStandalone target not found
Native extension for Android target not found
Native extension for WindowsStandalone target not found
Native extension for OSXStandalone target not found
Package Manager log level set to [2]
[Licensing::Client] Successfully resolved entitlement details
ScheduleIndexationOnStartup MainProcess:False IndexOnStartup:True
Mono: successfully reloaded assembly
Finished resetting current domain, in 11.015 seconds
Domain Reload Profiling: 11451ms
BeginReloadAssembly (125ms)
CreateAndSetChildDomain (16ms)
RebuildCommonClasses (37ms)
RebuildNativeTypeToScriptingClass (14ms)
initialDomainReloadingComplete (56ms)
LoadAllAssembliesAndSetupDomain (202ms)
LoadAssemblies (107ms)
AnalyzeDomain (191ms)
TypeCache.Refresh (189ms)
TypeCache.ScanAssembly (176ms)
FinalizeReload (11017ms)
SetupLoadedEditorAssemblies (0ms)
InitializePlatformSupportModulesInManaged (57ms)
BeforeProcessingInitializeOnLoad (85ms)
ProcessInitializeOnLoadAttributes (108ms)
ProcessInitializeOnLoadMethodAttributes (10700ms)
[Licensing::Client] Successfully resolved entitlement details
Application.AssetDatabase Initial Refresh Start
[Package Manager] Restoring resolved packages state from cache
[Licensing::Client] Successfully resolved entitlement details
[Package Manager] Registered 67 packages:
Packages from [https://packages.unity.com]:
com.unity.ai.navigation@2.0.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ai.navigation@78534c21b27d)
com.unity.addressables@2.7.6 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.addressables@45e9abf44299)
com.unity.collab-proxy@2.11.4 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.collab-proxy@a5329f833fa8)
com.unity.inputsystem@1.19.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.inputsystem@21a28c3a6c83)
com.unity.memoryprofiler@1.1.12 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.memoryprofiler@485b5ba42ef5)
com.unity.nuget.newtonsoft-json@3.2.2 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.nuget.newtonsoft-json@4dfd81071c64)
com.unity.probuilder@6.0.9 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.probuilder@1f279ab829b7)
com.unity.sharp-zip-lib@1.4.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.sharp-zip-lib@f6e4ef34e4d8)
com.unity.timeline@1.8.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.timeline@bfd27f8016ff)
com.unity.visualscripting@1.9.11 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.visualscripting@8bed5ad90189)
com.unity.searcher@4.9.4 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.searcher@d45a78918735)
com.unity.settings-manager@2.1.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.settings-manager@0b8638c5ce86)
com.unity.burst@1.8.28 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.burst@07790c2d06d9)
com.unity.mathematics@1.3.3 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.mathematics@19a9377c4ffa)
com.unity.profiling.core@1.0.3 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.profiling.core@8a49f7027d06)
com.unity.editorcoroutines@1.0.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.editorcoroutines@54394ed3283c)
com.unity.scriptablebuildpipeline@2.6.1 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.scriptablebuildpipeline@36e3b5898ee2)
com.unity.nuget.mono-cecil@1.11.6 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.nuget.mono-cecil@ecb9724e46ff)
com.unity.test-framework.performance@3.2.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.test-framework.performance@0840f58e4562)
Built-in packages:
com.unity.2d.sprite@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.2d.sprite@929df5adbb1f)
com.unity.render-pipelines.universal@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.universal@580a03820d50)
com.unity.ugui@2.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ugui@d8a2716f3013)
com.unity.modules.accessibility@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.accessibility)
com.unity.modules.ai@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.ai)
com.unity.modules.androidjni@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.androidjni)
com.unity.modules.animation@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.animation)
com.unity.modules.assetbundle@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.assetbundle)
com.unity.modules.audio@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.audio)
com.unity.modules.cloth@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.cloth)
com.unity.modules.director@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.director)
com.unity.modules.imageconversion@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.imageconversion)
com.unity.modules.imgui@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.imgui)
com.unity.modules.jsonserialize@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.jsonserialize)
com.unity.modules.particlesystem@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.particlesystem)
com.unity.modules.physics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.physics)
com.unity.modules.physics2d@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.physics2d)
com.unity.modules.screencapture@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.screencapture)
com.unity.modules.terrain@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.terrain)
com.unity.modules.terrainphysics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.terrainphysics)
com.unity.modules.tilemap@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.tilemap)
com.unity.modules.ui@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.ui)
com.unity.modules.uielements@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.uielements)
com.unity.modules.umbra@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.umbra)
com.unity.modules.unityanalytics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unityanalytics)
com.unity.modules.unitywebrequest@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequest)
com.unity.modules.unitywebrequestassetbundle@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestassetbundle)
com.unity.modules.unitywebrequestaudio@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestaudio)
com.unity.modules.unitywebrequesttexture@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequesttexture)
com.unity.modules.unitywebrequestwww@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.unitywebrequestwww)
com.unity.modules.vectorgraphics@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vectorgraphics)
com.unity.modules.vehicles@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vehicles)
com.unity.modules.video@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.video)
com.unity.modules.vr@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.vr)
com.unity.modules.wind@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.wind)
com.unity.modules.xr@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.xr)
com.unity.render-pipelines.core@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.core@e6c93b445dd3)
com.unity.modules.subsystems@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.subsystems)
com.unity.modules.hierarchycore@1.0.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.modules.hierarchycore)
com.unity.render-pipelines.universal-config@17.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.render-pipelines.universal-config@0db4263b9e6b)
com.unity.collections@6.4.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.collections@538ace9075bc)
com.unity.test-framework@1.6.0 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.test-framework@76560ee600cb)
com.unity.ext.nunit@2.0.5 (location: C:\hades\Hecton8\Library\PackageCache\com.unity.ext.nunit@d8c07649098d)
Embedded packages:
com.jbooth.microsplat.core@file:C:\hades\Hecton8\Packages\com.jbooth.microsplat.core (location: C:\hades\Hecton8\Packages\com.jbooth.microsplat.core)
com.jbooth.microsplat.urp2022@file:C:\hades\Hecton8\Packages\com.jbooth.microsplat.urp2022 (location: C:\hades\Hecton8\Packages\com.jbooth.microsplat.urp2022)
com.unity.shadergraph@file:C:\hades\Hecton8\Packages\com.unity.shadergraph (location: C:\hades\Hecton8\Packages\com.unity.shadergraph)
com.waveharmonic.crest@file:C:\hades\Hecton8\Packages\com.waveharmonic.crest (location: C:\hades\Hecton8\Packages\com.waveharmonic.crest)
Git packages:
com.coplaydev.unity-mcp@https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta (location: C:\hades\Hecton8\Library\PackageCache\com.coplaydev.unity-mcp@fbdb152757bd)
[Subsystems] No new subsystems found in resolved package list.
[Package Manager] Done registering packages in 0.01 seconds
[ScriptCompilation] Requested script compilation because: AssetDatabase observed changes in script compilation related files
Starting: C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Tools\BuildPipeline\bee_backend.exe --ipc --defer-dag-verification --dagfile="Library/Bee/1900b0aEDbg.dag" --continue-on-failure --profile="Library/Bee/backend1.traceevents" ScriptAssemblies
WorkingDir: C:/hades/Hecton8
DisplayProgressbar: Compiling Scripts
ExitCode: 0 Duration: 0s932ms
[2029/2784 0s] ILPP-Configuration Library/ilpp-configuration.nevergeneratedoutput
[2780/2784 0s] Csc Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.QA.Headless.Editor.dll (+2 others)
[2781/2784 0s] ILPostProcess Library/Bee/artifacts/1900b0aEDbg.dag/post-processed/Hecton8.QA.Headless.Editor.dll (+pdb)
Processing assembly Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.QA.Headless.Editor.dll, with 159 defines and 319 references
processors: Unity.Jobs.CodeGen.JobsILPostProcessor, zzzUnity.Burst.CodeGen.BurstILPostProcessor
running Unity.Jobs.CodeGen.JobsILPostProcessor
running zzzUnity.Burst.CodeGen.BurstILPostProcessor
[2782/2784 0s] CopyFiles Library/ScriptAssemblies/Hecton8.QA.Headless.Editor.dll
[2783/2784 0s] CopyFiles Library/ScriptAssemblies/Hecton8.QA.Headless.Editor.pdb
*** Tundra build success (0.90 seconds), 5 items updated, 2784 evaluated
AssetDatabase: script compilation time: 1.610345s
Begin MonoManager ReloadAssembly
Assembly DocCodeExamples.dll at Library/ScriptAssemblies/DocCodeExamples.dll not valid. Loading of assembly skipped.
Assembly Hecton8.World.Dots.dll at Library/ScriptAssemblies/Hecton8.World.Dots.dll not valid. Loading of assembly skipped.
Assembly TestNewCustomAssembly.dll at Library/ScriptAssemblies/TestNewCustomAssembly.dll not valid. Loading of assembly skipped.
Assembly Unity.2D.Sprite.Package.EditorTests.dll at Library/ScriptAssemblies/Unity.2D.Sprite.Package.EditorTests.dll not valid. Loading of assembly skipped.
Assembly Unity.Addressables.Editor.Tests.dll at Library/ScriptAssemblies/Unity.Addressables.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.Addressables.Runtime.Tests.dll at Library/ScriptAssemblies/Unity.Addressables.Runtime.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.Collections.BurstCompatibilityGen.dll at Library/ScriptAssemblies/Unity.Collections.BurstCompatibilityGen.dll not valid. Loading of assembly skipped.
Assembly Unity.Collections.DocCodeSamples.dll at Library/ScriptAssemblies/Unity.Collections.DocCodeSamples.dll not valid. Loading of assembly skipped.
Assembly Unity.Collections.PerformanceTests.dll at Library/ScriptAssemblies/Unity.Collections.PerformanceTests.dll not valid. Loading of assembly skipped.
Assembly Unity.Collections.Tests.dll at Library/ScriptAssemblies/Unity.Collections.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.Collections.Tests.Playmode.dll at Library/ScriptAssemblies/Unity.Collections.Tests.Playmode.dll not valid. Loading of assembly skipped.
Assembly Unity.InputSystem.IntegrationTests.dll at Library/ScriptAssemblies/Unity.InputSystem.IntegrationTests.dll not valid. Loading of assembly skipped.
Assembly Unity.Mathematics.Tests.dll at Library/ScriptAssemblies/Unity.Mathematics.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.Nuget.Mono-Cecil.dll at Library/ScriptAssemblies/Unity.Nuget.Mono-Cecil.dll not valid. Loading of assembly skipped.
Assembly Unity.PathTracing.Editor.Tests.dll at Library/ScriptAssemblies/Unity.PathTracing.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.PathTracing.Runtime.Tests.dll at Library/ScriptAssemblies/Unity.PathTracing.Runtime.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.RenderPipelines.Core.Editor.Tests.dll at Library/ScriptAssemblies/Unity.RenderPipelines.Core.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.RenderPipelines.Core.Runtime.Tests.dll at Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.RenderPipelines.Universal.Config.Editor.Tests.dll at Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Config.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.RenderPipelines.Universal.Editor.Tests.dll at Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.RenderPipelines.Universal.Runtime.Tests.dll at Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.ResourceManager.Tests.dll at Library/ScriptAssemblies/Unity.ResourceManager.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.ScriptableBuildPipeline.Editor.Tests.dll at Library/ScriptAssemblies/Unity.ScriptableBuildPipeline.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.ScriptableBuildPipeline.Tests.dll at Library/ScriptAssemblies/Unity.ScriptableBuildPipeline.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.ScriptableBuildPipelineTests.Runtime.Tests.dll at Library/ScriptAssemblies/Unity.ScriptableBuildPipelineTests.Runtime.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.Searcher.EditorTests.dll at Library/ScriptAssemblies/Unity.Searcher.EditorTests.dll not valid. Loading of assembly skipped.
Assembly Unity.Settings.Tests.dll at Library/ScriptAssemblies/Unity.Settings.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.ShaderGraph.Editor.Tests.dll at Library/ScriptAssemblies/Unity.ShaderGraph.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.SharpZipLib.Editor.Tests.dll at Library/ScriptAssemblies/Unity.SharpZipLib.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.SharpZipLib.Tests.dll at Library/ScriptAssemblies/Unity.SharpZipLib.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.TextMeshPro.Editor.Tests.dll at Library/ScriptAssemblies/Unity.TextMeshPro.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.TextMeshPro.Tests.dll at Library/ScriptAssemblies/Unity.TextMeshPro.Tests.dll not valid. Loading of assembly skipped.
Assembly Unity.UnifiedRayTracing.Editor.Tests.dll at Library/ScriptAssemblies/Unity.UnifiedRayTracing.Editor.Tests.dll not valid. Loading of assembly skipped.
Assembly UnityEditor.UI.Common.Tests.dll at Library/ScriptAssemblies/UnityEditor.UI.Common.Tests.dll not valid. Loading of assembly skipped.
Assembly UnityEditor.UI.EditorTests.dll at Library/ScriptAssemblies/UnityEditor.UI.EditorTests.dll not valid. Loading of assembly skipped.
Assembly UnityEngine.UI.Tests.dll at Library/ScriptAssemblies/UnityEngine.UI.Tests.dll not valid. Loading of assembly skipped.
Script error (PhysicsCullingTunerWindow): OnDrawGizmos() can not take parameters.
Script error (VerletTowTunerWindow): OnDrawGizmos() can not take parameters.
Script error (HullIntegrityTunerWindow): OnDrawGizmos() can not take parameters.
Script error (SubmarineDynoTunerWindow): OnDrawGizmos() can not take parameters.
Script error (BlackboxXRayViewer): OnDrawGizmos() can not take parameters.
Refreshing native plugins compatible for Editor in 4.54 ms, found 30 plugins.
Script error (SubmarineDynoTunerWindow): OnDrawGizmos() can not take parameters.
Script error (BlackboxXRayViewer): OnDrawGizmos() can not take parameters.
Script error (HullIntegrityTunerWindow): OnDrawGizmos() can not take parameters.
Script error (PhysicsCullingTunerWindow): OnDrawGizmos() can not take parameters.
Script error (VerletTowTunerWindow): OnDrawGizmos() can not take parameters.
Native extension for LinuxStandalone target not found
Native extension for Android target not found
Native extension for WindowsStandalone target not found
Native extension for OSXStandalone target not found
Refreshing native plugins compatible for Editor in 4.90 ms, found 30 plugins.
Launched and connected shader compiler UnityShaderCompiler.exe after 0.01 seconds
[MODES] ModeService[none].Initialize
[MODES] ModeService[none].LoadModes
[MODES] Loading mode Default (0) for mode-current-id-Submerge
ScheduleIndexationOnStartup MainProcess:True IndexOnStartup:True
Mono: successfully reloaded assembly
Finished resetting current domain, in 12.886 seconds
Domain Reload Profiling: 14467ms
BeginReloadAssembly (326ms)
DisableScriptedObjects (22ms)
CreateAndSetChildDomain (183ms)
RebuildCommonClasses (34ms)
RebuildNativeTypeToScriptingClass (13ms)
initialDomainReloadingComplete (80ms)
LoadAllAssembliesAndSetupDomain (1127ms)
LoadAssemblies (602ms)
AnalyzeDomain (599ms)
TypeCache.Refresh (450ms)
TypeCache.ScanAssembly (420ms)
BuildScriptInfoCaches (111ms)
ResolveRequiredComponents (33ms)
FinalizeReload (12887ms)
SetupLoadedEditorAssemblies (0ms)
InitializePlatformSupportModulesInManaged (37ms)
BeforeProcessingInitializeOnLoad (346ms)
ProcessInitializeOnLoadAttributes (10994ms)
ProcessInitializeOnLoadMethodAttributes (1140ms)
AfterProcessingInitializeOnLoad (6ms)
AwakeInstancesAfterBackupRestoration (8ms)
Refreshing native plugins compatible for Editor in 6.28 ms, found 30 plugins.
(Values over 0.050ms)
Asset Pipeline Refresh (id=4f7412481fea369458a2c77846e13310): Total: 17.722 seconds - Initiated by InitialRefreshV2(ForceSynchronousImport)
Summary:
Imports: total=0 (actual=0, local cache=0, cache server=0)
Asset DB Process Time: managed=0 ms, native=13881 ms
Asset DB Callback time: managed=403 ms, native=28 ms
Scripting: domain reloads=1, domain reload time=1587 ms, compile time=1612 ms, other=208 ms
Project Asset Count: scripts=3216, non-scripts=11453
Asset File Changes: new=0, changed=1, moved=0, deleted=0
Scan Filter Count: 0
InvokePackagesCallback: 15.857ms
ApplyChangesToAssetFolders: 1.663ms
Scan: 180.699ms
OnSourceAssetsModified: 0.836ms
CategorizeScriptCompilationAssets: 80.380ms
ProcessAssetsWithTransientArtifactChanges: 179.096ms
CategorizeAssets: 225.698ms
CompileScripts: 1612.112ms
ImportOutOfDateAssets: 12913.956ms (12905.572ms without children)
ReloadNativeAssets: 0.069ms
UnloadImportedAssets: 3.642ms
EnsureUptoDateAssetsAreRegisteredWithGuidPM: 3.305ms
OnDemandSchedulerStart: 1.361ms
PostProcessAllAssets: 405.502ms
XRBuildSystem.XRAssetImported 1.264ms
VCAutoAddAssetPostprocess 1.025ms
MonoPostProcessAllAssets: 402.648ms
StyleCatalogPostProcessor.OnPostprocessAllAssets 47.266ms
AssetPostprocessor.OnPostprocessAllAssets 14.444ms
UniversalRenderPipelineGlobalSettingsPostprocessor.OnPostprocessAllAssets 13.156ms
BuilderAssetPostprocessor.OnPostprocessAllAssets 4.438ms
SpeedTree9Postprocessor.OnPostprocessAllAssets 3.020ms
SyncVS.PostprocessSyncProject 2.405ms
WindowAssetPostprocessingWatcher.OnPostprocessAllAssets 1.997ms
TemplatePostProcessor.OnPostprocessAllAssets 1.938ms
ShaderGraphAssetPostProcessor.OnPostprocessAllAssets 1.900ms
AssetPostProcessor.OnPostprocessAllAssets 1.561ms
AssetChangedListener.OnPostprocessAllAssets 1.398ms
AddressablesAssetPostProcessor.OnPostprocessAllAssets 1.272ms
MaterialPostprocessor.OnPostprocessAllAssets 1.263ms
RetainedModeAssetPostprocessor.OnPostprocessAllAssets 1.246ms
HectonAssetIntegrityGuard.OnPostprocessAllAssets 0.939ms
SpriteEditorTexturePostprocessor.OnPostprocessAllAssets 0.931ms
UtilityWindowPostProcessor.OnPostprocessAllAssets 0.802ms
RenderPipelineGlobalSettingsPostprocessor.OnPostprocessAllAssets 0.779ms
InputActionAssetPostprocessor.OnPostprocessAllAssets 0.620ms
TextAssetPostProcessor.OnPostprocessAllAssets 0.618ms
ArtifactBrowserPostProcessor.OnPostprocessAllAssets 0.536ms
TextureArrayPreProcessor.OnPostprocessAllAssets 0.503ms
AssetPathToTypes.OnPostprocessAllAssets 0.490ms
InputActionJsonNameModifierAssetProcessor.OnPostprocessAllAssets 0.480ms
AssetEvents.OnPostprocessAllAssets 0.476ms
UVCSAssetPostprocessor.OnPostprocessAllAssets 0.475ms
SamplePostprocessor.OnPostprocessAllAssets 0.449ms
MatrixAssetTexturePostprocessor.OnPostprocessAllAssets 0.449ms
ReferencedClipsPostProcessor.OnPostprocessAllAssets 0.438ms
H8DataMonolithSourceWatcher.OnPostprocessAllAssets 0.373ms
TerrainToolbarOverlayPostProcessor.OnPostprocessAllAssets 0.359ms
TMPro_TexturePostProcessor.OnPostprocessAllAssets 0.355ms
PostProcessor.OnPostprocessAllAssets 0.333ms
SpeedTreePostProcessor.OnPostprocessAllAssets 0.306ms
AudioMixerPostprocessor.OnPostprocessAllAssets 0.304ms
ProjectSettingsPostprocessor.OnPostprocessAllAssets 0.258ms
AssetDatabaseCallbacks.OnPostprocessAllAssets 0.234ms
ConfigAssetsTracker.OnPostprocessAllAssets 0.197ms
ScenarioDriftAssetsTracker.OnPostprocessAllAssets 0.156ms
AudioContainerPostProcessor.OnPostprocessAllAssets 0.097ms
PostAssetChangesProfiler: 11.236ms
UnloadStreamsBegin: 1.758ms
PersistCurrentRevisions: 0.604ms
GenerateScriptTypeHashes: 19.067ms
GenerateScriptTypeSerializationHashes: 24.561ms
Untracked: 2089.366ms
Application.AssetDatabase Initial Refresh End
Shader Hidden/ChartRasterizerHardware is not supported: GPU does not support conservative rasterization
Scanning for USB devices : 0.759ms
Initializing Unity extensions:
GetVirtualKey: Could not map char: z (122) to any virtual key
GetVirtualKey: Could not map char: y (121) to any virtual key
GetVirtualKey: Could not map char: x (120) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: v (118) to any virtual key
GetVirtualKey: Could not map char: V (86) to any virtual key
GetVirtualKey: Could not map char: d (100) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: d (100) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: r (114) to any virtual key
GetVirtualKey: Could not map char: i (105) to any virtual key
GetVirtualKey: Could not map char: l (108) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: k (107) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: r (114) to any virtual key
GetVirtualKey: Could not map char: P (80) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: g (103) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: K (75) to any virtual key
GetVirtualKey: Could not map char: G (71) to any virtual key
GetVirtualKey: Could not map char: L (76) to any virtual key
GetVirtualKey: Could not map char: R (82) to any virtual key
GetVirtualKey: Could not map char: G (71) to any virtual key
GetVirtualKey: Could not map char: X (88) to any virtual key
GetVirtualKey: Could not map char: E (69) to any virtual key
GetVirtualKey: Could not map char: N (78) to any virtual key
GetVirtualKey: Could not map char: U (85) to any virtual key
GetVirtualKey: Could not map char: E (69) to any virtual key
GetVirtualKey: Could not map char: S (83) to any virtual key
GetVirtualKey: Could not map char: X (88) to any virtual key
GetVirtualKey: Could not map char: V (86) to any virtual key
GetVirtualKey: Could not map char: m (109) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: u (117) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: o (111) to any virtual key
GetVirtualKey: Could not map char: s (115) to any virtual key
GetVirtualKey: Could not map char: s (115) to any virtual key
GetVirtualKey: Could not map char: b (98) to any virtual key
GetVirtualKey: Could not map char: b (98) to any virtual key
Unloading 660 Unused Serialized files (Serialized files now loaded: 0)
Unloading 12650 unused Assets / (153.8 MB). Loaded Objects now: 12920.
Memory consumption went from 0.57 GB to 431.5 MB.
Total: 75.920200 ms (FindLiveObjects: 1.304700 ms CreateObjectMapping: 1.212600 ms MarkObjects: 34.449800 ms DeleteObjects: 38.949600 ms)
GetVirtualKey: Could not map char: z (122) to any virtual key
GetVirtualKey: Could not map char: y (121) to any virtual key
GetVirtualKey: Could not map char: x (120) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: v (118) to any virtual key
GetVirtualKey: Could not map char: V (86) to any virtual key
GetVirtualKey: Could not map char: d (100) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: d (100) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: r (114) to any virtual key
GetVirtualKey: Could not map char: i (105) to any virtual key
GetVirtualKey: Could not map char: l (108) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: k (107) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: p (112) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: r (114) to any virtual key
GetVirtualKey: Could not map char: P (80) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: g (103) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: f (102) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: (97) to any virtual key
GetVirtualKey: Could not map char: K (75) to any virtual key
GetVirtualKey: Could not map char: G (71) to any virtual key
GetVirtualKey: Could not map char: L (76) to any virtual key
GetVirtualKey: Could not map char: R (82) to any virtual key
GetVirtualKey: Could not map char: G (71) to any virtual key
GetVirtualKey: Could not map char: X (88) to any virtual key
GetVirtualKey: Could not map char: E (69) to any virtual key
GetVirtualKey: Could not map char: N (78) to any virtual key
GetVirtualKey: Could not map char: U (85) to any virtual key
GetVirtualKey: Could not map char: E (69) to any virtual key
GetVirtualKey: Could not map char: S (83) to any virtual key
GetVirtualKey: Could not map char: X (88) to any virtual key
GetVirtualKey: Could not map char: V (86) to any virtual key
GetVirtualKey: Could not map char: m (109) to any virtual key
GetVirtualKey: Could not map char: c (99) to any virtual key
GetVirtualKey: Could not map char: u (117) to any virtual key
GetVirtualKey: Could not map char: n (110) to any virtual key
GetVirtualKey: Could not map char: o (111) to any virtual key
GetVirtualKey: Could not map char: s (115) to any virtual key
GetVirtualKey: Could not map char: s (115) to any virtual key
GetVirtualKey: Could not map char: b (98) to any virtual key
GetVirtualKey: Could not map char: b (98) to any virtual key
Opening scene 'Assets/_Project/Scenes/00_BOOTSTRAP.unity'
Unloading 6 Unused Serialized files (Serialized files now loaded: 0)
Loaded scene 'Assets/_Project/Scenes/00_BOOTSTRAP.unity'
Deserialize: 4.357 ms
Integration: 384.420 ms
Integration of assets: 1.553 ms
Thread Wait Time: 0.133 ms
Total Operation Time: 390.462 ms
Unloading 4 unused Assets / (44.8 KB). Loaded Objects now: 13157.
Memory consumption went from 292.9 MB to 292.8 MB.
Total: 34.611600 ms (FindLiveObjects: 0.581800 ms CreateObjectMapping: 0.532000 ms MarkObjects: 33.370900 ms DeleteObjects: 0.125400 ms)
Microsoft Media Foundation video decoding to texture disabled: graphics device is Null, only Direct3D 11 and Direct3D 12 (only on desktop) are supported for hardware-accelerated video decoding.
[Project] Loading completed in 33.939 seconds
Project init time: 31.996 seconds
Template init time: 0.000 seconds
Package Manager init time: 0.993 seconds
Asset Database init time: 0.692 seconds
Global illumination init time: 0.002 seconds
Assemblies load time: 11.485 seconds
Unity extensions init time: 0.121 seconds
Asset Database refresh time: 17.723 seconds
Scene opening time: 1.609 seconds
utp: "type":"ProjectInfo","version":2,"phase":"Immediate","time":1779092331055,"processId":40220,"projectLoad":33.9390546,"projectInit":31.996281,"templateInit":0.0,"packageManagerInit":0.9929887,"assetDatabaseInit":0.691657,"globalIlluminationInit":0.0018679,"assembliesLoad":11.4845087,"unityExtensionsInit":0.12056,"assetDatabaseRefresh":17.7228846,"sceneOpening":1.6091983
utp: "type":"EditorInfo","version":2,"phase":"Immediate","time":1779092331055,"processId":40220,"editorVersion":"6000.4.1f1 (8535861f39e1)","branch":"6000.4/staging","buildType":"Release","platform":"Windows"
Entering Playmode with Reload Domain disabled.
If you experience any issues, please disable "Enter Play Mode Options" in Editor Settings
[GlobalRegistry] SystemDispatcher is not registered. Bootstrap must create and register it before runtime tick registration.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace ()
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:LogError (object)
Hecton8.Core.GlobalRegistry:TryEnsureDispatcherRegistration () (at Assets/_Project/Scripts/Core/GlobalRegistry.cs:6361)
Hecton8.Core.GlobalRegistry:TryRegisterLateFrameTickable (Hecton8.Core.ILateFrameTickable,Hecton8.Core.PriorityLayer) (at Assets/_Project/Scripts/Core/GlobalRegistry.cs:5994)
Hecton8.Audio.Prologue.PrologueAcousticOrchestrator:OnEnable () (at Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:85)
(Filename: Assets/_Project/Scripts/Core/GlobalRegistry.cs Line: 6361)
[BiomeBoundarySdfRuntimeBootstrap] Spawned BiomeBoundarySdfRuntime at runtime because active scene had none. Owner='BiomeBoundarySdfRuntime_Root'. This is fail-safe, not substitute for authored setup.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace ()
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:LogWarning (object)
Hecton8.World.Biomes.BiomeBoundarySdfRuntimeBootstrap:EnsureRuntimeInstance () (at Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntimeBootstrap.cs:26)
(Filename: Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntimeBootstrap.cs Line: 26)
Asset Pipeline Refresh (id=2e8c482de93fee44abe2afaccd493247): Total: 0.181 seconds - Initiated by RefreshV2(NoUpdateAssetOptions)
Asset Pipeline Refresh (id=34b5c81630f5bbb479fb230736803a19): Total: 0.015 seconds - Initiated by RefreshV2(NoUpdateAssetOptions)
Loaded scene 'Temp/__Backupscenes/0.backup'
Deserialize: 1.738 ms
Integration: 2408.397 ms
Integration of assets: 0.002 ms
Thread Wait Time: 0.034 ms
Total Operation Time: 2410.171 ms
Asset Pipeline Refresh (id=f2445e9eabe10974daa8ac3574796375): Total: 0.142 seconds - Initiated by RefreshV2(NoUpdateAssetOptions)
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Client] Successfully resolved entitlement details
Start Indexing on Editor startup
[Indexing] Starting Initial Indexing for Assets
[MemoryBudgetTracker] HomeostasisBrain exceeded persistent budget. Used=0.02 MB, Budget=0.01 MB.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace ()
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:LogWarning (object)
Hecton8.Core.MemoryBudgetTracker:Register (string,long,long) (at Assets/_Project/Scripts/Core/MemoryBudgetTracker.cs:51)
Hecton8.Core.HomeostasisBrain:InitializeRuntime () (at Assets/_Project/Scripts/Core/HomeostasisBrain.cs:267)
Hecton8.Core.SystemDispatcher:InitializeService () (at Assets/_Project/Scripts/Core/SystemDispatcher.cs:1869)
Hecton8.Bootstrap.GameBootstrapper:EnsureSystemDispatcherRegistered () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2743)
Hecton8.Bootstrap.GameBootstrapper:InitializeBootstrapDependencyNode (Hecton8.Bootstrap.GameBootstrapper/BootstrapDependencyNode) (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2567)
Hecton8.Bootstrap.GameBootstrapper:TryInitializeBootstrapDependencyNodeWithFallback (Hecton8.Bootstrap.GameBootstrapper/BootstrapDependencyNode) (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2523)
Hecton8.Bootstrap.GameBootstrapper/ d__273:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2358)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__273>:DoMoveNext ()
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :Start d__273> (Hecton8.Bootstrap.GameBootstrapper/ d__273&)
Hecton8.Bootstrap.GameBootstrapper:InitializeBootstrapLayerNodesAsync (Hecton8.Bootstrap.GameBootstrapper/BootstrapPhase,System.Threading.CancellationToken)
Hecton8.Bootstrap.GameBootstrapper/ d__254:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1903)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__254>:DoMoveNext ()
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :Start d__254> (Hecton8.Bootstrap.GameBootstrapper/ d__254&)
Hecton8.Bootstrap.GameBootstrapper:InitializeCoreLayerAsync (System.Threading.CancellationToken)
Hecton8.Bootstrap.GameBootstrapper/ d__243:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1669)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__243>:DoMoveNext ()
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :Start d__243> (Hecton8.Bootstrap.GameBootstrapper/ d__243&)
Hecton8.Bootstrap.GameBootstrapper:InitializeCoreServicesPhaseAsync (System.Threading.CancellationToken)
Hecton8.Bootstrap.GameBootstrapper/ d__227:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1350)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__227>:DoMoveNext ()
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :Start d__227> (Hecton8.Bootstrap.GameBootstrapper/ d__227&)
Hecton8.Bootstrap.GameBootstrapper:RunBootstrapPhaseAsync (Hecton8.Bootstrap.GameBootstrapper/BootstrapPhase,Hecton8.Core.BootstrapStepToken,System.Func`2 >,System.Threading.CancellationToken)
Hecton8.Bootstrap.GameBootstrapper/ d__225:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1265)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__225>:DoMoveNext ()
UnityEngine.Awaitable:RunOrScheduleContinuation (UnityEngine.Awaitable/AwaiterCompletionThreadAffinity,System.Action)
UnityEngine.Awaitable:RaiseManagedCompletion ()
UnityEngine.Awaitable`1 :SetResultAndRaiseContinuation (bool)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :SetResult (bool)
Hecton8.Bootstrap.GameBootstrapper/ d__227:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1380)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__227>:DoMoveNext ()
UnityEngine.Awaitable:RunOrScheduleContinuation (UnityEngine.Awaitable/AwaiterCompletionThreadAffinity,System.Action)
UnityEngine.Awaitable:RaiseManagedCompletion ()
UnityEngine.Awaitable`1 :SetResultAndRaiseContinuation (bool)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1 :SetResult (bool)
Hecton8.Bootstrap.GameBootstrapper/ d__230:MoveNext () (at Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1466)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder`1/StateMachineBox`1 d__230>:DoMoveNext ()
UnityEngine.Awaitable:RunOrScheduleContinuation (UnityEngine.Awaitable/AwaiterCompletionThreadAffinity,System.Action)
UnityEngine.Awaitable:RaiseManagedCompletion ()
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder:SetResult ()
Hecton8.Core.AwaitableDebtMonitor/ d__11:MoveNext () (at Assets/_Project/Scripts/Core/InputDispatcher.cs:3768)
UnityEngine.Awaitable/AwaitableAsyncMethodBuilder/StateMachineBox`1 d__11>:DoMoveNext ()
UnityEngine.Awaitable:RunOrScheduleContinuation (UnityEngine.Awaitable/AwaiterCompletionThreadAffinity,System.Action)
UnityEngine.Awaitable:RaiseManagedCompletion ()
UnityEngine.Awaitable/DoubleBufferedAwaitableList:SwapAndComplete ()
UnityEngine.Awaitable:OnUpdate ()
(Filename: Assets/_Project/Scripts/Core/MemoryBudgetTracker.cs Line: 51)
Compilation was requested for method `Hecton8.Core.HomeostasisBrain, Hecton8.Core, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::ComputeSystemHealthIndexBurst(System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Hecton8.Core.HomeostasisBrain, Hecton8.Core, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::ComputeFrameEwmaBurst(System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.WatchdogStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.WatchdogStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.TelemetrySnapshotDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.TelemetrySnapshotDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38InputStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38InputStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38RouteWaypointDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38RouteWaypointDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.MockRebaseSignal, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.MockRebaseSignal, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38TuningDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38TuningDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38MockVaultDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38MockVaultDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38WatchdogTelemetryEntry, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38WatchdogTelemetryEntry, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[System.Byte, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[System.Byte, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriteCommand, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriteCommand, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriterStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriterStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriterCursorDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38FileWriterCursorDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Compilation was requested for method `Unity.Jobs.IJobExtensions+JobStruct`1[[Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38WaypointIngestStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null::Execute(Hecton8.QA.Headless.Shinobu38QaWatchdogRuntime+Shinobu38MemClearJob`1[[Hecton8.QA.Headless.Shinobu38WaypointIngestStateDTO, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]&, Hecton8.QA.Headless, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 System.IntPtr, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 Unity.Jobs.LowLevel.Unsafe.JobRanges&, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089)` but it is not known Burst entry point. This may be because [BurstCompile] method is defined in generic class, and generic class is not instantiated with concrete types anywhere in your code.
Created GICache directory at C:/Users/danat/AppData/LocalLow/Unity/Caches/GiCache. Took: 0.021s, timestamps: [36.604 - 36.625]
===== END FILE: Unity_SHINOBU_38_Run_final_exitprocess.log =====
