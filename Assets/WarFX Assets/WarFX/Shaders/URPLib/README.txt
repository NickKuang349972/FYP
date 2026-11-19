本目录（URPLib/）包含 Unity Universal RP 14.0.12 与 Core RP 14.0.12 的 ShaderLibrary HLSL 子集
（含 com.unity.render-pipelines.universal-config 的 ShaderConfig.cs.hlsl 与 core 包的
Hammersley.cs.hlsl），随包分发以便工程在**未安装 URP 包**时（Built-in 模板工程）也能正常
导入与编译本包 Shader。URP 环境下这些文件与管线包提供的版本等效（同版本源码）。

来源：com.unity.render-pipelines.universal@14.0.12 / com.unity.render-pipelines.core@14.0.12 /
com.unity.render-pipelines.universal-config@14.0.10（Unity 官方包，© 2022 Unity Technologies，
适用其随包分发的许可条款）。仅作兼容性分发，未做功能修改（仅重写 include 相对路径）。
