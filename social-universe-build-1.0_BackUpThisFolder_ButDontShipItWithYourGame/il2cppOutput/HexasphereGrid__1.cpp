#include "pch-cpp.hpp"





struct VirtualActionInvoker0
{
	typedef void (*Action)(void*, const RuntimeMethod*);

	static inline void Invoke (Il2CppMethodSlot slot, RuntimeObject* obj)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		((Action)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename T1>
struct VirtualActionInvoker1
{
	typedef void (*Action)(void*, T1, const RuntimeMethod*);

	static inline void Invoke (Il2CppMethodSlot slot, RuntimeObject* obj, T1 p1)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		((Action)invokeData.methodPtr)(obj, p1, invokeData.method);
	}
};
template <typename R>
struct VirtualFuncInvoker0
{
	typedef R (*Func)(void*, const RuntimeMethod*);

	static inline R Invoke (Il2CppMethodSlot slot, RuntimeObject* obj)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		return ((Func)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename R, typename T1, typename T2>
struct VirtualFuncInvoker2
{
	typedef R (*Func)(void*, T1, T2, const RuntimeMethod*);

	static inline R Invoke (Il2CppMethodSlot slot, RuntimeObject* obj, T1 p1, T2 p2)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_virtual_invoke_data(slot, obj);
		return ((Func)invokeData.methodPtr)(obj, p1, p2, invokeData.method);
	}
};
struct GenericVirtualActionInvoker0
{
	typedef void (*Action)(void*, const RuntimeMethod*);

	static inline void Invoke (const RuntimeMethod* method, RuntimeObject* obj)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData);
		((Action)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename T1>
struct GenericVirtualActionInvoker1
{
	typedef void (*Action)(void*, T1, const RuntimeMethod*);

	static inline void Invoke (const RuntimeMethod* method, RuntimeObject* obj, T1 p1)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData);
		((Action)invokeData.methodPtr)(obj, p1, invokeData.method);
	}
};
template <typename R>
struct GenericVirtualFuncInvoker0
{
	typedef R (*Func)(void*, const RuntimeMethod*);

	static inline R Invoke (const RuntimeMethod* method, RuntimeObject* obj)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData);
		return ((Func)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename R, typename T1, typename T2>
struct GenericVirtualFuncInvoker2
{
	typedef R (*Func)(void*, T1, T2, const RuntimeMethod*);

	static inline R Invoke (const RuntimeMethod* method, RuntimeObject* obj, T1 p1, T2 p2)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData);
		return ((Func)invokeData.methodPtr)(obj, p1, p2, invokeData.method);
	}
};
struct InterfaceActionInvoker0
{
	typedef void (*Action)(void*, const RuntimeMethod*);

	static inline void Invoke (Il2CppMethodSlot slot, RuntimeClass* declaringInterface, RuntimeObject* obj)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_interface_invoke_data(slot, obj, declaringInterface);
		((Action)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename T1>
struct InterfaceActionInvoker1
{
	typedef void (*Action)(void*, T1, const RuntimeMethod*);

	static inline void Invoke (Il2CppMethodSlot slot, RuntimeClass* declaringInterface, RuntimeObject* obj, T1 p1)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_interface_invoke_data(slot, obj, declaringInterface);
		((Action)invokeData.methodPtr)(obj, p1, invokeData.method);
	}
};
template <typename R>
struct InterfaceFuncInvoker0
{
	typedef R (*Func)(void*, const RuntimeMethod*);

	static inline R Invoke (Il2CppMethodSlot slot, RuntimeClass* declaringInterface, RuntimeObject* obj)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_interface_invoke_data(slot, obj, declaringInterface);
		return ((Func)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename R, typename T1, typename T2>
struct InterfaceFuncInvoker2
{
	typedef R (*Func)(void*, T1, T2, const RuntimeMethod*);

	static inline R Invoke (Il2CppMethodSlot slot, RuntimeClass* declaringInterface, RuntimeObject* obj, T1 p1, T2 p2)
	{
		const VirtualInvokeData& invokeData = il2cpp_codegen_get_interface_invoke_data(slot, obj, declaringInterface);
		return ((Func)invokeData.methodPtr)(obj, p1, p2, invokeData.method);
	}
};
struct GenericInterfaceActionInvoker0
{
	typedef void (*Action)(void*, const RuntimeMethod*);

	static inline void Invoke (const RuntimeMethod* method, RuntimeObject* obj)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData);
		((Action)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename T1>
struct GenericInterfaceActionInvoker1
{
	typedef void (*Action)(void*, T1, const RuntimeMethod*);

	static inline void Invoke (const RuntimeMethod* method, RuntimeObject* obj, T1 p1)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData);
		((Action)invokeData.methodPtr)(obj, p1, invokeData.method);
	}
};
template <typename R>
struct GenericInterfaceFuncInvoker0
{
	typedef R (*Func)(void*, const RuntimeMethod*);

	static inline R Invoke (const RuntimeMethod* method, RuntimeObject* obj)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData);
		return ((Func)invokeData.methodPtr)(obj, invokeData.method);
	}
};
template <typename R, typename T1, typename T2>
struct GenericInterfaceFuncInvoker2
{
	typedef R (*Func)(void*, T1, T2, const RuntimeMethod*);

	static inline R Invoke (const RuntimeMethod* method, RuntimeObject* obj, T1 p1, T2 p2)
	{
		VirtualInvokeData invokeData;
		il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData);
		return ((Func)invokeData.methodPtr)(obj, p1, p2, invokeData.method);
	}
};

struct Dictionary_2_t636F9C070769C139CD799C938BCE87A25215D571;
struct Dictionary_2_t48E78A307BF6EF41AF0546E1DA208EA7822F98B6;
struct Dictionary_2_t01224C8DBCCFE276E97D2BF52F4D7B10D3642682;
struct Dictionary_2_tBF325E0F09BEEDF7AC6E6CB85841301637FC6E90;
struct Dictionary_2_t6D8BD97C276122733C26FB7272D62F5675961A11;
struct Dictionary_2_tFC9DF1E7D180CC0F09043B63D3B0E3B33CD25F83;
struct IComparer_1_t4483F9B9F43C7B0F8D4FEEAE12FAFDD3F9CF81FD;
struct List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73;
struct List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D;
struct List_1_tE8D7CADB79D7B89DE79B80D7F9C56526C93D9F3D;
struct List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F;
struct List_1_t0F231C3F13EBA1FF9081BD61489D01AA3CBE59D4;
struct List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5;
struct List_1U5BU5D_t4CE1B0D94CF35C0CCBCA465FCD6362B44614AB71;
struct List_1U5BU5D_t37294D7C303231F2FD83B3C398AED0937F4F3206;
struct List_1U5BU5D_tDE88DA8DCD79A37A10DCC96911E1242D15FF66FE;
struct List_1U5BU5D_tC1B009E92641A2C993F3BB28A80D61D2AB67979B;
struct List_1U5BU5D_t9BF6AD8E40F61ECF42793E072C32B96F7717E274;
struct BooleanU5BU5D_tD317D27C31DB892BE79FAE3AEBC0B3FFB73DE9B4;
struct ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031;
struct CharU5BU5D_t799905CF001DD5F13F7DBB310181FC4D8B7D0AAB;
struct ColorU5BU5D_t612261CF293F6FFC3D80AB52259FF0DC2B2CC389;
struct DelegateU5BU5D_tC5AB7E8F745616680F337909D3A8E6C722CDF771;
struct Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C;
struct MeshU5BU5D_t178CA36422FC397211E68FB7E39C5B2F95619689;
struct MeshFilterU5BU5D_tCE3B457E6F7ECE5ECEE9E09150642150448685BA;
struct MeshRendererU5BU5D_tDF429EF168050A5CE085D0B51909A6AE2067E446;
struct ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918;
struct PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B;
struct PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2;
struct StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248;
struct Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191;
struct TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806;
struct TileSaveDataU5BU5D_t7511B87ACD9E4A3C65DBDAFCE03C39DE3E2C6153;
struct TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452;
struct Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA;
struct Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C;
struct AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C;
struct Camera_tA92CC927D7439999BC82DBEDC0AA45B470F9E184;
struct CancellationTokenSource_tAAE1E0033BCFC233801F8CB4CED5C852B350CB7B;
struct Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3;
struct Delegate_t;
struct DelegateData_t9B286B493293CD2D23A5B2B5EF0E5B1324C2B77E;
struct GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206;
struct Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC;
struct HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A;
struct HexasphereSaveData_tE176CBF1E9D43C2C71D732FB61214E29E3909846;
struct IAsyncResult_t7B9B5A0ECB35DCEC31B8A8122C37D687369253B5;
struct Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3;
struct MethodInfo_t;
struct MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71;
struct Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C;
struct PFNodesComparer_t2721E614A3AB471BBCC4CB6CDB3E9CEB9071B513;
struct PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38;
struct Point_t13126743CEDB2A83E25B6018553E5022E06D2790;
struct RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27;
struct Renderer_t320575F223BCB177A982E5DDB5DB19FAA89E7FBF;
struct SphereCollider_tBA111C542CE97F6873DE742757213D6265C7D275;
struct String_t;
struct Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700;
struct Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4;
struct Texture2DArray_t5ADB8D23A8AA2F2F3916F43852194B78E579E6BA;
struct TextureScaler_t17AC3C253E6114048501AC8E81E36A0B0111AE2F;
struct Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67;
struct Transform_tB27202C6F4E36D225EE28A13E4D662BF99785DB1;
struct Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F;
struct UnitySourceGeneratedAssemblyMonoScriptTypes_v1_t3ECE4AEC156020A8212642B1F162C353BE9ABEEB;
struct Void_t4861ACF8F4594C3437BB48B6E56783494B843915;
struct HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF;
struct PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9;
struct TileEvent_t3392B77898A6708FA7D695CF027BB60332242782;

IL2CPP_EXTERN_C RuntimeClass* Application_tDB03BE91CDF0ACA614A5E0B67CFB77C44EB19B21_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Debug_t8394C7EEAECA3689C2C9B9DE9C7166D73596276F_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Graphics_t99CD970FFEA58171C70F54DF0C06D315BD452F2C_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* IComparer_1_t4483F9B9F43C7B0F8D4FEEAE12FAFDD3F9CF81FD_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Math_tEB65DE7CA8B083C412C969C92981C030865486CE_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeClass* Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3_il2cpp_TypeInfo_var;
IL2CPP_EXTERN_C RuntimeField* U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____603FC505D8667A39DDA6F8B988C09A307B128A8459995AA82761F35797FC50FF_FieldInfo_var;
IL2CPP_EXTERN_C RuntimeField* U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____F02AE59C30131A8E71EF5AD251B5E2D9B48B6D031F267523C2642A177C734295_FieldInfo_var;
IL2CPP_EXTERN_C String_t* _stringLiteral04AD164E2A4DE9935B205DCA02B5501342A39890;
IL2CPP_EXTERN_C String_t* _stringLiteral16A31016B4ED8ACC43060D56B4167B4F84B62186;
IL2CPP_EXTERN_C String_t* _stringLiteral1DD9A9C5EC5E22754998A64514F4804E700D8942;
IL2CPP_EXTERN_C String_t* _stringLiteral47A3FAF17D89549FD0F0ECA7370B81F7C80DFCDE;
IL2CPP_EXTERN_C String_t* _stringLiteral4B8146FB95E4F51B29DA41EB5F6D60F8FD0ECF21;
IL2CPP_EXTERN_C String_t* _stringLiteral51282E2AAC09AC6EDBC2C1C237C0183F97FEE379;
IL2CPP_EXTERN_C String_t* _stringLiteral59861356BAB5171272E157858059C1801D7D5E5D;
IL2CPP_EXTERN_C String_t* _stringLiteral6586EC4CA6BDC3EEC4B0F6A15908751430DE99EE;
IL2CPP_EXTERN_C String_t* _stringLiteral65DAFD279CC322209A6F3D846D770AA652BE1F34;
IL2CPP_EXTERN_C String_t* _stringLiteral67BEC592386C17C68CF044FFB14169A1073AC7EB;
IL2CPP_EXTERN_C String_t* _stringLiteral787984D270B549500FD6EE450785085D7058DF70;
IL2CPP_EXTERN_C String_t* _stringLiteralAA5D14A3F019134EF42083FAC4AFA3DD9DAF0B04;
IL2CPP_EXTERN_C String_t* _stringLiteralC18C9BB6DF0D5C60CE5A5D2D3D6111BEB6F8CCEB;
IL2CPP_EXTERN_C const RuntimeMethod* Component_GetComponent_TisHexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC_m1AB88AA716F1C4F1ED1B562648F98C5330FEC7B3_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Add_m7B178FDE6A5885D6C5CA3B7B4526898D85E95FA2_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Clear_mEA2D1EBD5CD934C78BB6B4022108C7CF1EB32C98_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1_ToArray_mA192205F4E984425407DF97AF1E772728F7BDB51_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1__ctor_m04CF8E658DFD6C00F15510DD05E2CE000175075E_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98_RuntimeMethod_var;
IL2CPP_EXTERN_C const RuntimeMethod* List_1__ctor_m47D3709632F94FA2260DFD6A32BF6B3A095A451D_RuntimeMethod_var;
struct Delegate_t_marshaled_com;
struct Delegate_t_marshaled_pinvoke;

struct ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031;
struct DelegateU5BU5D_tC5AB7E8F745616680F337909D3A8E6C722CDF771;
struct Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C;
struct ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918;
struct PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B;
struct PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2;
struct StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248;
struct Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191;
struct TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806;
struct TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452;
struct Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C;

IL2CPP_EXTERN_C_BEGIN
IL2CPP_EXTERN_C_END

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
struct U3CModuleU3E_t046F60AEDD50DA33D52F65F3A66A98C78272ACC3 
{
};
struct List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73  : public RuntimeObject
{
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ____items;
	int32_t ____size;
	int32_t ____version;
	RuntimeObject* ____syncRoot;
};
struct List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D  : public RuntimeObject
{
	ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* ____items;
	int32_t ____size;
	int32_t ____version;
	RuntimeObject* ____syncRoot;
};
struct List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F  : public RuntimeObject
{
	PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* ____items;
	int32_t ____size;
	int32_t ____version;
	RuntimeObject* ____syncRoot;
};
struct List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5  : public RuntimeObject
{
	TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* ____items;
	int32_t ____size;
	int32_t ____version;
	RuntimeObject* ____syncRoot;
};
struct U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D  : public RuntimeObject
{
};
struct HexasphereSaveData_tE176CBF1E9D43C2C71D732FB61214E29E3909846  : public RuntimeObject
{
	TileSaveDataU5BU5D_t7511B87ACD9E4A3C65DBDAFCE03C39DE3E2C6153* ___tiles;
};
struct Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779  : public RuntimeObject
{
};
struct PFNodesComparer_t2721E614A3AB471BBCC4CB6CDB3E9CEB9071B513  : public RuntimeObject
{
	PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* ___m;
};
struct PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38  : public RuntimeObject
{
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___tiles;
	RuntimeObject* ___mComparer;
	int32_t ___tilesCount;
};
struct ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101  : public RuntimeObject
{
};
struct String_t  : public RuntimeObject
{
	int32_t ____stringLength;
	Il2CppChar ____firstChar;
};
struct TextureScaler_t17AC3C253E6114048501AC8E81E36A0B0111AE2F  : public RuntimeObject
{
};
struct Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F  : public RuntimeObject
{
	PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* ___points;
	int32_t ___getOrderedFlag;
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___centroid;
	bool ___centroIdComputed;
};
struct UnitySourceGeneratedAssemblyMonoScriptTypes_v1_t3ECE4AEC156020A8212642B1F162C353BE9ABEEB  : public RuntimeObject
{
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F  : public RuntimeObject
{
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F_marshaled_pinvoke
{
};
struct ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F_marshaled_com
{
};
struct Boolean_t09A6377A54BE2F9E6985A8149F19234FD7DDFE22 
{
	bool ___m_value;
};
struct Byte_t94D9231AC217BE4D2E004C4CD32DF6D099EA41A3 
{
	uint8_t ___m_value;
};
struct Color_tD001788D726C3A7F1379BEED0260B9591F440C1F 
{
	float ___r;
	float ___g;
	float ___b;
	float ___a;
};
struct Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B 
{
	union
	{
		#pragma pack(push, tp, 1)
		struct
		{
			int32_t ___rgba;
		};
		#pragma pack(pop, tp)
		struct
		{
			int32_t ___rgba_forAlignmentOnly;
		};
		#pragma pack(push, tp, 1)
		struct
		{
			uint8_t ___r;
		};
		#pragma pack(pop, tp)
		struct
		{
			uint8_t ___r_forAlignmentOnly;
		};
		#pragma pack(push, tp, 1)
		struct
		{
			char ___g_OffsetPadding[1];
			uint8_t ___g;
		};
		#pragma pack(pop, tp)
		struct
		{
			char ___g_OffsetPadding_forAlignmentOnly[1];
			uint8_t ___g_forAlignmentOnly;
		};
		#pragma pack(push, tp, 1)
		struct
		{
			char ___b_OffsetPadding[2];
			uint8_t ___b;
		};
		#pragma pack(pop, tp)
		struct
		{
			char ___b_OffsetPadding_forAlignmentOnly[2];
			uint8_t ___b_forAlignmentOnly;
		};
		#pragma pack(push, tp, 1)
		struct
		{
			char ___a_OffsetPadding[3];
			uint8_t ___a;
		};
		#pragma pack(pop, tp)
		struct
		{
			char ___a_OffsetPadding_forAlignmentOnly[3];
			uint8_t ___a_forAlignmentOnly;
		};
	};
};
struct Double_tE150EF3D1D43DEE85D533810AB4C742307EEDE5F 
{
	double ___m_value;
};
struct Enum_t2A1A94B24E3B776EEF4E5E485E290BB9D4D072E2  : public ValueType_t6D9B272BD21782F0A9A14F2E41F85A50E97A986F
{
};
struct Enum_t2A1A94B24E3B776EEF4E5E485E290BB9D4D072E2_marshaled_pinvoke
{
};
struct Enum_t2A1A94B24E3B776EEF4E5E485E290BB9D4D072E2_marshaled_com
{
};
struct Int32_t680FF22E76F6EFAD4375103CBBFFA0421349384C 
{
	int32_t ___m_value;
};
struct IntPtr_t 
{
	void* ___m_value;
};
struct PFClosedNode_t9F3487DDFBAD01E5B11F753574779FBC30024FCF 
{
	float ___f;
	float ___g;
	int32_t ___index;
	int32_t ___prevIndex;
};
struct PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A 
{
	float ___f;
	float ___g;
	int32_t ___prevIndex;
	uint8_t ___status;
};
struct Quaternion_tDA59F214EF07D7700B26E40E562F267AF7306974 
{
	float ___x;
	float ___y;
	float ___z;
	float ___w;
};
struct Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D 
{
	float ___m_XMin;
	float ___m_YMin;
	float ___m_Width;
	float ___m_Height;
};
struct Single_t4530F2FF86FCB0DC29F35385CA1BD21BE294761C 
{
	float ___m_value;
};
struct Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 
{
	float ___x;
	float ___y;
};
struct Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 
{
	float ___x;
	float ___y;
	float ___z;
};
struct Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 
{
	float ___x;
	float ___y;
	float ___z;
	float ___w;
};
struct Void_t4861ACF8F4594C3437BB48B6E56783494B843915 
{
	union
	{
		struct
		{
		};
		uint8_t Void_t4861ACF8F4594C3437BB48B6E56783494B843915__padding[1];
	};
};
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D1061_t7E29C4150308B4F832BCAF46B9E57E7BEE8E3BA2 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D1061_t7E29C4150308B4F832BCAF46B9E57E7BEE8E3BA2__padding[1061];
	};
};
#pragma pack(pop, tp)
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D36_tAC6F03FFAB40CA91570382AF5152A5729674BF58 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D36_tAC6F03FFAB40CA91570382AF5152A5729674BF58__padding[36];
	};
};
#pragma pack(pop, tp)
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D48_t368B4C3BB464A5130E38DFD2FE4494AB66EE77DB 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D48_t368B4C3BB464A5130E38DFD2FE4494AB66EE77DB__padding[48];
	};
};
#pragma pack(pop, tp)
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D60_t510F19EC55F6A2F6EC814A132C90559DCDE52D1C 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D60_t510F19EC55F6A2F6EC814A132C90559DCDE52D1C__padding[60];
	};
};
#pragma pack(pop, tp)
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D664_t0B90588951505A513B3131D50337EF9B19F4D22F 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D664_t0B90588951505A513B3131D50337EF9B19F4D22F__padding[664];
	};
};
#pragma pack(pop, tp)
#pragma pack(push, tp, 1)
struct __StaticArrayInitTypeSizeU3D72_tCF0C9841AFBB805720B080DD7231D4DE192FEF4E 
{
	union
	{
		struct
		{
			union
			{
			};
		};
		uint8_t __StaticArrayInitTypeSizeU3D72_tCF0C9841AFBB805720B080DD7231D4DE192FEF4E__padding[72];
	};
};
#pragma pack(pop, tp)
struct MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29 
{
	ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* ___FilePathsData;
	ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* ___TypesData;
	int32_t ___TotalTypes;
	int32_t ___TotalFiles;
	bool ___IsEditorOnly;
};
struct MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_pinvoke
{
	Il2CppSafeArray* ___FilePathsData;
	Il2CppSafeArray* ___TypesData;
	int32_t ___TotalTypes;
	int32_t ___TotalFiles;
	int32_t ___IsEditorOnly;
};
struct MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_com
{
	Il2CppSafeArray* ___FilePathsData;
	Il2CppSafeArray* ___TypesData;
	int32_t ___TotalTypes;
	int32_t ___TotalFiles;
	int32_t ___IsEditorOnly;
};
struct Delegate_t  : public RuntimeObject
{
	intptr_t ___method_ptr;
	intptr_t ___invoke_impl;
	RuntimeObject* ___m_target;
	intptr_t ___method;
	intptr_t ___delegate_trampoline;
	intptr_t ___extra_arg;
	intptr_t ___method_code;
	intptr_t ___interp_method;
	intptr_t ___interp_invoke_impl;
	MethodInfo_t* ___method_info;
	MethodInfo_t* ___original_method_info;
	DelegateData_t9B286B493293CD2D23A5B2B5EF0E5B1324C2B77E* ___data;
	bool ___method_is_virtual;
};
struct Delegate_t_marshaled_pinvoke
{
	intptr_t ___method_ptr;
	intptr_t ___invoke_impl;
	Il2CppIUnknown* ___m_target;
	intptr_t ___method;
	intptr_t ___delegate_trampoline;
	intptr_t ___extra_arg;
	intptr_t ___method_code;
	intptr_t ___interp_method;
	intptr_t ___interp_invoke_impl;
	MethodInfo_t* ___method_info;
	MethodInfo_t* ___original_method_info;
	DelegateData_t9B286B493293CD2D23A5B2B5EF0E5B1324C2B77E* ___data;
	int32_t ___method_is_virtual;
};
struct Delegate_t_marshaled_com
{
	intptr_t ___method_ptr;
	intptr_t ___invoke_impl;
	Il2CppIUnknown* ___m_target;
	intptr_t ___method;
	intptr_t ___delegate_trampoline;
	intptr_t ___extra_arg;
	intptr_t ___method_code;
	intptr_t ___interp_method;
	intptr_t ___interp_invoke_impl;
	MethodInfo_t* ___method_info;
	MethodInfo_t* ___original_method_info;
	DelegateData_t9B286B493293CD2D23A5B2B5EF0E5B1324C2B77E* ___data;
	int32_t ___method_is_virtual;
};
struct FilterMode_t4AD57F1A3FE272D650E0E688BA044AE872BD2A34 
{
	int32_t ___value__;
};
struct HIGHLIGHT_STYLE_t9F5F3F42D15C8E63A6398FE914DD53AF271DBBAF 
{
	int32_t ___value__;
};
struct HeuristicFormula_t1AA38B23DC2BD6032EA3832384B730271902F742 
{
	int32_t ___value__;
};
struct Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C  : public RuntimeObject
{
	intptr_t ___m_CachedPtr;
};
struct Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_marshaled_pinvoke
{
	intptr_t ___m_CachedPtr;
};
struct Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_marshaled_com
{
	intptr_t ___m_CachedPtr;
};
struct Point_t13126743CEDB2A83E25B6018553E5022E06D2790  : public RuntimeObject
{
	float ___x;
	float ___y;
	float ___z;
	float ____elevation;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ____projectedVector3;
	bool ____projectedVector3Computed;
	TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* ___triangles;
	int32_t ___triangleCount;
	Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* ___tile;
	int32_t ___hashCode;
};
struct ROTATION_AXIS_ALLOWED_t26C34C8B0AA1AED626DB4D4060A15D9680E59502 
{
	int32_t ___value__;
};
struct Ray_t2B1742D7958DC05BDC3EFC7461D3593E1430DC00 
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___m_Origin;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___m_Direction;
};
struct RuntimeFieldHandle_t6E4C45B6D2EA12FC99185805A7E77527899B25C5 
{
	intptr_t ___value;
};
struct STYLE_t83C01E12C73156896000F96741FD4C5FA443835A 
{
	int32_t ___value__;
};
struct Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67  : public RuntimeObject
{
	int32_t ___index;
	PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* ___vertexPoints;
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___centerPoint;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___center;
	int32_t ___borders;
	bool ___isWater;
	bool ___canCross;
	int32_t ___group;
	float ___rotation;
	Renderer_t320575F223BCB177A982E5DDB5DB19FAA89E7FBF* ___renderer;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ___customMat;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ___tempMat;
	float ___extrudeAmount;
	int32_t ___uvShadedChunkIndex;
	int32_t ___uvShadedChunkStart;
	int32_t ___uvShadedChunkLength;
	int32_t ___uvWireChunkIndex;
	int32_t ___uvWireChunkStart;
	int32_t ___uvWireChunkLength;
	float ___heightMapValue;
	String_t* ___tag;
	int32_t ___tagInt;
	bool ___visible;
	float ___crossCost;
	float ___computedCrossCost;
	Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* ____vertices;
	bool ____verticesComputed;
	TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* ____neighbours;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ____neighboursIndices;
	bool ____neighboursComputed;
};
struct TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F 
{
	int32_t ___tileIndex;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___color;
	int32_t ___textureIndex;
	String_t* ___tag;
	int32_t ___tagInt;
};
struct TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_pinvoke
{
	int32_t ___tileIndex;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___color;
	int32_t ___textureIndex;
	char* ___tag;
	int32_t ___tagInt;
};
struct TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_com
{
	int32_t ___tileIndex;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___color;
	int32_t ___textureIndex;
	Il2CppChar* ___tag;
	int32_t ___tagInt;
};
struct Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3  : public Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C
{
};
struct MulticastDelegate_t  : public Delegate_t
{
	DelegateU5BU5D_tC5AB7E8F745616680F337909D3A8E6C722CDF771* ___delegates;
};
struct MulticastDelegate_t_marshaled_pinvoke : public Delegate_t_marshaled_pinvoke
{
	Delegate_t_marshaled_pinvoke** ___delegates;
};
struct MulticastDelegate_t_marshaled_com : public Delegate_t_marshaled_com
{
	Delegate_t_marshaled_com** ___delegates;
};
struct Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700  : public Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C
{
};
struct AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C  : public MulticastDelegate_t
{
};
struct Behaviour_t01970CFBBA658497AE30F311C447DB0440BAB7FA  : public Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3
{
};
struct GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206  : public MulticastDelegate_t
{
};
struct RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27  : public Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700
{
};
struct Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4  : public Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700
{
};
struct HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF  : public MulticastDelegate_t
{
};
struct PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9  : public MulticastDelegate_t
{
};
struct TileEvent_t3392B77898A6708FA7D695CF027BB60332242782  : public MulticastDelegate_t
{
};
struct MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71  : public Behaviour_t01970CFBBA658497AE30F311C447DB0440BAB7FA
{
	CancellationTokenSource_tAAE1E0033BCFC233801F8CB4CED5C852B350CB7B* ___m_CancellationTokenSource;
};
struct Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC  : public MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71
{
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____tileShadedFrameMatBevel;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____tileShadedFrameMatExtrusion;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____tileShadedFrameMatNoExtrusion;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____gridMatExtrusion;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____gridMatNoExtrusion;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____tileColoredMat;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ____tileTexturedMat;
	Material_t18053F08F347D0DCA5E1140EC7EC4533DD8A14E3* ___highlightMaterial;
	int32_t ___currentDivisions;
	int32_t ___currentTextureSize;
	bool ___currentExtruded;
	bool ___currentInvertedMode;
	bool ___currentWireframeColorFromTile;
	bool ___currentSmartEdges;
	bool ___currentBevel;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___currentDefaultShadedColor;
	bool ___pendingUVUpdateFast;
	bool ___pendingTextureArrayUpdate;
	bool ___pendingColorsUpdate;
	int32_t ___currentStyle;
	float ___currentTransparencyTiles;
	bool ___mouseIsOver;
	bool ___mouseStartedDragging;
	bool ___hasDragged;
	float ___clickStart;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___mouseDragStartLocalPosition;
	float ___wheelAccel;
	Quaternion_tDA59F214EF07D7700B26E40E562F267AF7306974 ___flyingStartRotation;
	Quaternion_tDA59F214EF07D7700B26E40E562F267AF7306974 ___flyingEndRotation;
	bool ___flying;
	float ___flyingStartTime;
	float ___flyingDuration;
	Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___defaultRampTexture;
	SphereCollider_tBA111C542CE97F6873DE742757213D6265C7D275* ___sphereCollider;
	int32_t ___lastHitTileIndex;
	Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___whiteTex;
	int32_t ___uvChunkCount;
	int32_t ___wireChunkCount;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___currentRotationShift;
	bool ___leftMouseButtonClick;
	bool ___leftMouseButtonPressed;
	bool ___leftMouseButtonRelease;
	bool ___rightMouseButtonPressed;
	bool ___allowedTextureArray;
	bool ___useEditorRay;
	Ray_t2B1742D7958DC05BDC3EFC7461D3593E1430DC00 ___editorRay;
	bool ___shouldUpdateMaterialProperties;
	bool ___needRegenerate;
	bool ___needRegenerateWireframe;
	Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___bevelNormals;
	ColorU5BU5D_t612261CF293F6FFC3D80AB52259FF0DC2B2CC389* ___bevelNormalsColors;
	List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* ___tmpList;
	Dictionary_2_t01224C8DBCCFE276E97D2BF52F4D7B10D3642682* ___tmpDict;
	List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* ___tmpCandidates;
	int32_t ___lastHoverTileIndex;
	bool ___canInteract;
	Texture2DArray_t5ADB8D23A8AA2F2F3916F43852194B78E579E6BA* ___finalTexArray;
	bool ___triggerOnGenerateEvent;
	Dictionary_2_tFC9DF1E7D180CC0F09043B63D3B0E3B33CD25F83* ___points;
	Dictionary_2_t6D8BD97C276122733C26FB7272D62F5675961A11* ___verticesIdx;
	List_1U5BU5D_tC1B009E92641A2C993F3BB28A80D61D2AB67979B* ___verticesWire;
	List_1U5BU5D_t37294D7C303231F2FD83B3C398AED0937F4F3206* ___indicesWire;
	List_1U5BU5D_tDE88DA8DCD79A37A10DCC96911E1242D15FF66FE* ___uvWire;
	List_1U5BU5D_t4CE1B0D94CF35C0CCBCA465FCD6362B44614AB71* ___colorWire;
	List_1U5BU5D_tC1B009E92641A2C993F3BB28A80D61D2AB67979B* ___verticesShaded;
	List_1U5BU5D_t37294D7C303231F2FD83B3C398AED0937F4F3206* ___indicesShaded;
	List_1U5BU5D_t9BF6AD8E40F61ECF42793E072C32B96F7717E274* ___uvShaded;
	List_1U5BU5D_t9BF6AD8E40F61ECF42793E072C32B96F7717E274* ___uv2Shaded;
	List_1U5BU5D_t4CE1B0D94CF35C0CCBCA465FCD6362B44614AB71* ___colorShaded;
	List_1_t0F231C3F13EBA1FF9081BD61489D01AA3CBE59D4* ___texArray;
	Dictionary_2_t48E78A307BF6EF41AF0546E1DA208EA7822F98B6* ___solidTexCache;
	MeshU5BU5D_t178CA36422FC397211E68FB7E39C5B2F95619689* ___shadedMeshes;
	MeshFilterU5BU5D_tCE3B457E6F7ECE5ECEE9E09150642150448685BA* ___shadedMFs;
	MeshRendererU5BU5D_tDF429EF168050A5CE085D0B51909A6AE2067E446* ___shadedMRs;
	MeshU5BU5D_t178CA36422FC397211E68FB7E39C5B2F95619689* ___wiredMeshes;
	MeshFilterU5BU5D_tCE3B457E6F7ECE5ECEE9E09150642150448685BA* ___wiredMFs;
	MeshRendererU5BU5D_tDF429EF168050A5CE085D0B51909A6AE2067E446* ___wiredMRs;
	BooleanU5BU5D_tD317D27C31DB892BE79FAE3AEBC0B3FFB73DE9B4* ___colorShadedDirty;
	BooleanU5BU5D_tD317D27C31DB892BE79FAE3AEBC0B3FFB73DE9B4* ___uvShadedDirty;
	BooleanU5BU5D_tD317D27C31DB892BE79FAE3AEBC0B3FFB73DE9B4* ___uvWireDirty;
	BooleanU5BU5D_tD317D27C31DB892BE79FAE3AEBC0B3FFB73DE9B4* ___colorWireDirty;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___oldCameraPosition;
	Transform_tB27202C6F4E36D225EE28A13E4D662BF99785DB1* ___tilesRoot;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___hexagonIndices;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___hexagonUVs;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___hexagonUVsInverted;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___hexagonIndicesExtruded;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___hexagonUVsExtruded;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___hexagonIndicesInverted;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___pentagonIndices;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___pentagonUVs;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___pentagonUVsInverted;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___pentagonIndicesExtruded;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___pentagonUVsExtruded;
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___pentagonIndicesInverted;
	Dictionary_2_t636F9C070769C139CD799C938BCE87A25215D571* ___colorCache;
	Dictionary_2_tBF325E0F09BEEDF7AC6E6CB85841301637FC6E90* ___textureCache;
	Vector2U5BU5D_tFEBBC94BCC6C9C88277BA04047D2B3FDB6ED7FDA* ___uvTmp;
	float ___lastTimeCheckVRPointers;
	bool ___needRefreshRouteMatrix;
	PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* ___open;
	PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* ___pfCalc;
	uint8_t ___openTileValue;
	uint8_t ___closeTileValue;
	List_1_tE8D7CADB79D7B89DE79B80D7F9C56526C93D9F3D* ___close;
	int32_t ___mSearchLimit;
	bool ___mIgnoreTileCanCross;
	int32_t ___lastRouteMatrixGroupMask;
	ColorU5BU5D_t612261CF293F6FFC3D80AB52259FF0DC2B2CC389* ___heights;
	ColorU5BU5D_t612261CF293F6FFC3D80AB52259FF0DC2B2CC389* ___waters;
	int32_t ___heightMapWidth;
	int32_t ___heightMapHeight;
	ColorU5BU5D_t612261CF293F6FFC3D80AB52259FF0DC2B2CC389* ___gradientColors;
	int32_t ___rampWidth;
	int32_t ____numDivisions;
	int32_t ____style;
	bool ____smartEdges;
	bool ____transparent;
	bool ____transparencyZWrite;
	float ____transparencyTiles;
	bool ____transparencyCull;
	bool ____transparencyDoubleSided;
	bool ____invertedMode;
	bool ____lighting;
	bool ____castShadows;
	bool ____receiveShadows;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____ambientColor;
	float ____minimumLight;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____specularTint;
	float ____smoothness;
	bool ____extruded;
	bool ____bevel;
	int32_t ____tileTextureSize;
	bool ____tileTextureStretch;
	float ____extrudeMultiplier;
	bool ____VREnabled;
	float ____gradientIntensity;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____wireframeColor;
	bool ____wireframeColorFromTile;
	float ____wireframeIntensity;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____defaultShadedColor;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____tileTintColor;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ____rotationShift;
	bool ____enableGridEditor;
	Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191* ___textures;
	PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* ___OnPathFindingCrossTile;
	TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* ___OnTileClick;
	TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* ___OnTileMouseOver;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnFlyStart;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnFlyEnd;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnDragStart;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnDragEnd;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnZoom;
	HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* ___OnGeneration;
	Camera_tA92CC927D7439999BC82DBEDC0AA45B470F9E184* ____cameraMain;
	bool ____respectOtherUI;
	bool ____rotationEnabled;
	float ____rotationSpeed;
	int32_t ____rotationAxisAllowed;
	float ____rotationAxisVerticalThreshold;
	bool ____zoomEnabled;
	float ____zoomSpeed;
	float ____zoomDamping;
	float ____zoomMinDistance;
	float ____zoomMaxDistance;
	float ____flyToTilt;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ____highlightColor;
	float ____highlightSpeed;
	int32_t ____highlightStyle;
	bool ____highlightEnabled;
	bool ____raycast3D;
	float ____dragThreshold;
	float ____clickDuration;
	bool ____rightButtonDrag;
	bool ____rightClickRotates;
	bool ____rightClickRotatingClockwise;
	int32_t ___lastHighlightedTileIndex;
	Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* ___lastHighlightedTile;
	int32_t ___lastClickedTile;
	int32_t ____pathFindingHeuristicFormula;
	int32_t ____pathFindingSearchLimit;
	bool ____pathFindingUseExtrusion;
	int32_t ____pathFindingExtrusionWeight;
	TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* ___tiles;
};
struct HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A  : public MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71
{
	String_t* ___info;
	String_t* ___title;
	String_t* ___config;
	Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191* ___textures;
};
struct List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73_StaticFields
{
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* ___s_emptyArray;
};
struct List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D_StaticFields
{
	ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* ___s_emptyArray;
};
struct List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F_StaticFields
{
	PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* ___s_emptyArray;
};
struct List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5_StaticFields
{
	TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* ___s_emptyArray;
};
struct U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D_StaticFields
{
	__StaticArrayInitTypeSizeU3D36_tAC6F03FFAB40CA91570382AF5152A5729674BF58 ___20816112DC2748D47641A620FC25D1BAAB2144EDD11F390DF9E74E5CC62AEC93;
	__StaticArrayInitTypeSizeU3D36_tAC6F03FFAB40CA91570382AF5152A5729674BF58 ___25DD2C3220D833E94BF73322F00F2496F15EE01109A0C43004141BDB1B19776E;
	__StaticArrayInitTypeSizeU3D1061_t7E29C4150308B4F832BCAF46B9E57E7BEE8E3BA2 ___603FC505D8667A39DDA6F8B988C09A307B128A8459995AA82761F35797FC50FF;
	__StaticArrayInitTypeSizeU3D60_t510F19EC55F6A2F6EC814A132C90559DCDE52D1C ___904D09F5E2D3759C292A77122BD9749D1D9FD9A4445C76F0B31E6401A383AD15;
	__StaticArrayInitTypeSizeU3D48_t368B4C3BB464A5130E38DFD2FE4494AB66EE77DB ___AA90DAA9658172F888CC465E68CFCADB9CF95A5F2B9A7DD5D861BDA85ABF50EB;
	__StaticArrayInitTypeSizeU3D48_t368B4C3BB464A5130E38DFD2FE4494AB66EE77DB ___C6628225904FBD0E0369A60C3528E88598A102911D0DEFCCCD051B0CDDDCE912;
	__StaticArrayInitTypeSizeU3D664_t0B90588951505A513B3131D50337EF9B19F4D22F ___F02AE59C30131A8E71EF5AD251B5E2D9B48B6D031F267523C2642A177C734295;
	__StaticArrayInitTypeSizeU3D72_tCF0C9841AFBB805720B080DD7231D4DE192FEF4E ___FF16ED65DD078C4204A7D118C55530786F3B6E784804467469FCCD48B0CC0602;
};
struct Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___Vector4zero;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___Vector3one;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___Vector3zero;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___Vector3up;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___Vector2one;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___Vector2zero;
	Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B ___Color32White;
};
struct ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields
{
	int32_t ___MainTex;
	int32_t ___BaseMap;
	int32_t ___Color2;
	int32_t ___Color;
	int32_t ___BaseColor;
	int32_t ___TileAlpha;
	int32_t ___ColorShift;
	int32_t ___Center;
};
struct String_t_StaticFields
{
	String_t* ___Empty;
};
struct Boolean_t09A6377A54BE2F9E6985A8149F19234FD7DDFE22_StaticFields
{
	String_t* ___TrueString;
	String_t* ___FalseString;
};
struct IntPtr_t_StaticFields
{
	intptr_t ___Zero;
};
struct Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D_StaticFields
{
	Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D ___kZero;
};
struct Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_StaticFields
{
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___zeroVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___oneVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___upVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___downVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___leftVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___rightVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___positiveInfinityVector;
	Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 ___negativeInfinityVector;
};
struct Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_StaticFields
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___zeroVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___oneVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___upVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___downVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___leftVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___rightVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___forwardVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___backVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___positiveInfinityVector;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___negativeInfinityVector;
};
struct Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3_StaticFields
{
	Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___zeroVector;
	Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___oneVector;
	Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___positiveInfinityVector;
	Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___negativeInfinityVector;
};
struct Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_StaticFields
{
	int32_t ___OffsetOfInstanceIDInCPlusPlusObject;
};
struct Point_t13126743CEDB2A83E25B6018553E5022E06D2790_StaticFields
{
	int32_t ___flag;
};
struct Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields
{
	TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* ___tempTriangles;
	List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* ___tempInt;
	List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* ___temp;
};
struct Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700_StaticFields
{
	int32_t ___GenerateAllMips;
};
#ifdef __clang__
#pragma clang diagnostic pop
#endif
struct ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031  : public RuntimeArray
{
	ALIGN_FIELD (8) uint8_t m_Items[1];

	inline uint8_t GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline uint8_t* GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, uint8_t value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
	}
	inline uint8_t GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline uint8_t* GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, uint8_t value)
	{
		m_Items[index] = value;
	}
};
struct DelegateU5BU5D_tC5AB7E8F745616680F337909D3A8E6C722CDF771  : public RuntimeArray
{
	ALIGN_FIELD (8) Delegate_t* m_Items[1];

	inline Delegate_t* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Delegate_t** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Delegate_t* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline Delegate_t* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Delegate_t** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Delegate_t* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B  : public RuntimeArray
{
	ALIGN_FIELD (8) PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A m_Items[1];

	inline PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A* GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
	}
	inline PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A* GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, PFNodeFast_t733E00D4460F96FA6F4BCB901079F6431CF16D4A value)
	{
		m_Items[index] = value;
	}
};
struct Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C  : public RuntimeArray
{
	ALIGN_FIELD (8) int32_t m_Items[1];

	inline int32_t GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline int32_t* GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, int32_t value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
	}
	inline int32_t GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline int32_t* GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, int32_t value)
	{
		m_Items[index] = value;
	}
};
struct TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452  : public RuntimeArray
{
	ALIGN_FIELD (8) Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* m_Items[1];

	inline Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248  : public RuntimeArray
{
	ALIGN_FIELD (8) String_t* m_Items[1];

	inline String_t* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline String_t** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, String_t* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline String_t* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline String_t** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, String_t* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2  : public RuntimeArray
{
	ALIGN_FIELD (8) Point_t13126743CEDB2A83E25B6018553E5022E06D2790* m_Items[1];

	inline Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Point_t13126743CEDB2A83E25B6018553E5022E06D2790** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Point_t13126743CEDB2A83E25B6018553E5022E06D2790** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C  : public RuntimeArray
{
	ALIGN_FIELD (8) Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 m_Items[1];

	inline Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2* GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
	}
	inline Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2* GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 value)
	{
		m_Items[index] = value;
	}
};
struct TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806  : public RuntimeArray
{
	ALIGN_FIELD (8) Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* m_Items[1];

	inline Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191  : public RuntimeArray
{
	ALIGN_FIELD (8) Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* m_Items[1];

	inline Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};
struct ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918  : public RuntimeArray
{
	ALIGN_FIELD (8) RuntimeObject* m_Items[1];

	inline RuntimeObject* GetAt(il2cpp_array_size_t index) const
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items[index];
	}
	inline RuntimeObject** GetAddressAt(il2cpp_array_size_t index)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		return m_Items + index;
	}
	inline void SetAt(il2cpp_array_size_t index, RuntimeObject* value)
	{
		IL2CPP_ARRAY_BOUNDS_CHECK(index, (uint32_t)(this)->max_length);
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
	inline RuntimeObject* GetAtUnchecked(il2cpp_array_size_t index) const
	{
		return m_Items[index];
	}
	inline RuntimeObject** GetAddressAtUnchecked(il2cpp_array_size_t index)
	{
		return m_Items + index;
	}
	inline void SetAtUnchecked(il2cpp_array_size_t index, RuntimeObject* value)
	{
		m_Items[index] = value;
		Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
	}
};


IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void List_1__ctor_m76CBBC3E2F0583F5AD30CE592CEA1225C06A0428_gshared (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, int32_t ___0_capacity, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Add_mEBCF994CC3814631017F46A387B1A192ED6C85C7_gshared_inline (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, RuntimeObject* ___0_item, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_gshared_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Clear_m16C1F2C61FED5955F10EB36BC1CB2DF34B128994_gshared_inline (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B_gshared (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_gshared_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* List_1_ToArray_mD7E4F8E7C11C3C67CB5739FCC0A6E86106A6291F_gshared (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA_gshared (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98_gshared (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_capacity, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* Component_GetComponent_TisRuntimeObject_m7181F81CAEC2CF53F5D2BC79B7425C16E1F80D33_gshared (Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_NO_INLINE IL2CPP_METHOD_ATTR void List_1_AddWithResize_m79A9BF770BEF9C06BE40D5401E55E375F2726CC4_gshared (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, RuntimeObject* ___0_item, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_NO_INLINE IL2CPP_METHOD_ATTR void List_1_AddWithResize_m378B392086AAB6F400944FA9839516326B3F7BB8_gshared (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method) ;

IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void RuntimeHelpers_InitializeArray_m751372AA3F24FBF6DA9B9D687CBFA2DE436CAB9B (RuntimeArray* ___0_array, RuntimeFieldHandle_t6E4C45B6D2EA12FC99185805A7E77527899B25C5 ___1_fldHandle, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2 (RuntimeObject* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t PQInt_Compare_mBF5FC828D5E4D31492590E496A610F5070E972D0 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, int32_t ___0_i, int32_t ___1_j, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PQInt_Swap_m567F5C190448FABE06702552EB2885D7556020E9 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, int32_t ___0_i, int32_t ___1_j, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 Vector4_get_zero_m3D61F5FA9483CD9C08977D9D8852FB448B4CE6D1_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector4_op_Implicit_m0217ADDC8CADDB93ACBABB17A50207698DAB0071_inline (Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___0_v, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_one_mC9B289F1E15C42C597180C9FE6FB492495B51D02_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_zero_m0C1249C3F25B1C70EAD3CC8B31259975A457AE39_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_up_m128AF3FDC820BF59D5DE86D973E7DE3F20C3AEBA_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 Vector2_get_one_m9097EB8DC23C26118A591AF16702796C3EF51DFB_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 Vector2_get_zero_m32506C40EC2EE7D5D4410BF40D3EE683A3D5F32C_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Color_tD001788D726C3A7F1379BEED0260B9591F440C1F Color_get_white_m068F5AF879B0FCA584E3693F762EA41BB65532C6_inline (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B Color32_op_Implicit_m79AF5E0BDE9CE041CAC4D89CBFA66E71C6DD1B70_inline (Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___0_c, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_ComputeProjectedVertex_m48B50AB8903161CF2286AB0D4DFBD74987AE50D3 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) ;
inline void List_1__ctor_m04CF8E658DFD6C00F15510DD05E2CE000175075E (List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* __this, int32_t ___0_capacity, const RuntimeMethod* method)
{
	((  void (*) (List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F*, int32_t, const RuntimeMethod*))List_1__ctor_m76CBBC3E2F0583F5AD30CE592CEA1225C06A0428_gshared)(__this, ___0_capacity, method);
}
inline void List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_inline (List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_item, const RuntimeMethod* method)
{
	((  void (*) (List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F*, Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*))List_1_Add_mEBCF994CC3814631017F46A387B1A192ED6C85C7_gshared_inline)(__this, ___0_item, method);
}
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, float ___0_x, float ___1_y, float ___2_z, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_inline (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Vector3__ctor_m376936E6B999EF1ECBE57D990A386303E2283DE0_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2* __this, float ___0_x, float ___1_y, float ___2_z, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Triangle_isAdjacentTo_m7CF316F8E00DE3432EAA5C9C71C70AC2694FB94B (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* __this, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* ___0_tri2, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR String_t* Single_ToString_mE282EDA9CA4F7DF88432D807732837A629D04972 (float* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR String_t* String_Concat_m647EBF831F54B6DF7D5AFA5FD012CF4EE7571B6A (StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* ___0_values, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2 (float* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA (String_t* ___0_name, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* RenderTexture_get_active_mA4434B3E79DEF2C01CAE0A53061598B16443C9E7 (const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Rect__ctor_m18C3033D135097BEE424AAA68D91C706D2647F23_inline (Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D* __this, float ___0_x, float ___1_y, float ___2_width, float ___3_height, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TextureScaler__gpu_scale_m22CBF203D8BC668F378F1B797C02F61EB8624B60 (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___0_src, int32_t ___1_width, int32_t ___2_height, int32_t ___3_fmode, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Texture2D_Reinitialize_m9AB4169DA359C18BB4102F8E00C4321B53714E6B (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* __this, int32_t ___0_width, int32_t ___1_height, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Texture2D_ReadPixels_m7483DB211233F02E46418E9A6077487925F0024C (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* __this, Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D ___0_source, int32_t ___1_destX, int32_t ___2_destY, bool ___3_recalculateMipMaps, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Texture2D_Apply_mCC369BCAB2D3AD3EE50EE01DA67AF227865FA2B3 (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* __this, bool ___0_updateMipmaps, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void RenderTexture_set_active_m5EE8E2327EF9B306C1425014CC34C41A8384E7AB (RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* ___0_value, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Texture_set_filterMode_mE423E58C0C16D059EA62BA87AD70F44AEA50CCC9 (Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700* __this, int32_t ___0_value, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void RenderTexture__ctor_m45EACC89DDF408948889586516B3CA7AA8B73BFA (RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* __this, int32_t ___0_width, int32_t ___1_height, int32_t ___2_depth, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Graphics_SetRenderTarget_m995C0F14B97C5BF46CCF2E7EF410C1CC05C46409 (RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* ___0_rt, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void GL_LoadPixelMatrix_mF1C5A4508C5F110512C116A5DDE7AB0483FE961A (float ___0_left, float ___1_right, float ___2_bottom, float ___3_top, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Color__ctor_m3786F0D6E510D9CFA544523A955870BD2A514C8C_inline (Color_tD001788D726C3A7F1379BEED0260B9591F440C1F* __this, float ___0_r, float ___1_g, float ___2_b, float ___3_a, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void GL_Clear_mA172E771FC32B516DB826F537832307C3A16BE09 (bool ___0_clearDepth, bool ___1_clearColor, Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___2_backgroundColor, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Graphics_DrawTexture_m400F92CB13445A7BC054BC074B7073EA7E4B322F (Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D ___0_screenRect, Texture_t791CBB51219779964E0E8A2ED7C1AA5F92A4A700* ___1_texture, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile_ComputeVertices_mF2997F9195BAE507B4FC1541BE4C5A0042CDDA32 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile_ComputeNeighbours_mD86CFDBC54BCB4BD622E5CAEE85E22DBDDEECEEC (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Point_get_projectedVector3_m173ED0275B0A7F93BCE5B23F34BFA602C68F33D6 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Point_GetOrderedTriangles_m7F19272FCADBE86F98D99E4A5AC259F94122CC1B (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* ___0_tempTriangles, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* Triangle_GetCentroid_mA3FC4743A9681A58A97A61FEA04E4CE2D88C57DD (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* __this, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_a, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_b, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_Cross_mF93A280558BCE756D13B6CC5DCD7DE8A43148987_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_lhs, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_rhs, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR float Vector3_Dot_mBB86BB940AA0A32FA7D3C02AC42E5BC7095A5D52_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_lhs, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_rhs, const RuntimeMethod* method) ;
inline void List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, const RuntimeMethod* method)
{
	((  void (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, const RuntimeMethod*))List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_gshared_inline)(__this, method);
}
inline void List_1_Clear_mEA2D1EBD5CD934C78BB6B4022108C7CF1EB32C98_inline (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* __this, const RuntimeMethod* method)
{
	((  void (*) (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5*, const RuntimeMethod*))List_1_Clear_m16C1F2C61FED5955F10EB36BC1CB2DF34B128994_gshared_inline)(__this, method);
}
inline bool List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method)
{
	return ((  bool (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, int32_t, const RuntimeMethod*))List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B_gshared)(__this, ___0_item, method);
}
inline void List_1_Add_m7B178FDE6A5885D6C5CA3B7B4526898D85E95FA2_inline (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* __this, Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* ___0_item, const RuntimeMethod* method)
{
	((  void (*) (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5*, Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67*, const RuntimeMethod*))List_1_Add_mEBCF994CC3814631017F46A387B1A192ED6C85C7_gshared_inline)(__this, ___0_item, method);
}
inline void List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method)
{
	((  void (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, int32_t, const RuntimeMethod*))List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_gshared_inline)(__this, ___0_item, method);
}
inline TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* List_1_ToArray_mA192205F4E984425407DF97AF1E772728F7BDB51 (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* __this, const RuntimeMethod* method)
{
	return ((  TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* (*) (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5*, const RuntimeMethod*))List_1_ToArray_mD7E4F8E7C11C3C67CB5739FCC0A6E86106A6291F_gshared)(__this, method);
}
inline Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, const RuntimeMethod* method)
{
	return ((  Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, const RuntimeMethod*))List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA_gshared)(__this, method);
}
inline void List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98 (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_capacity, const RuntimeMethod* method)
{
	((  void (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, int32_t, const RuntimeMethod*))List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98_gshared)(__this, ___0_capacity, method);
}
inline void List_1__ctor_m47D3709632F94FA2260DFD6A32BF6B3A095A451D (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* __this, int32_t ___0_capacity, const RuntimeMethod* method)
{
	((  void (*) (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5*, int32_t, const RuntimeMethod*))List_1__ctor_m76CBBC3E2F0583F5AD30CE592CEA1225C06A0428_gshared)(__this, ___0_capacity, method);
}
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_RegisterTriangle_mF61506CB9B7560D76421D17A8BF1757FB75EDD4C (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* ___0_triangle, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Application_get_isPlaying_m25B0ABDFEF54F5370CD3F263A813540843D00F34 (const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void MonoBehaviour_Invoke_mF724350C59362B0F1BFE26383209A274A29A63FB (MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71* __this, String_t* ___0_methodName, float ___1_time, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereConfig_LoadConfiguration_m64902A2456E137780C2D549C81B698459F6418C8 (HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A* __this, const RuntimeMethod* method) ;
inline Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* Component_GetComponent_TisHexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC_m1AB88AA716F1C4F1ED1B562648F98C5330FEC7B3 (Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3* __this, const RuntimeMethod* method)
{
	return ((  Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* (*) (Component_t39FBE53E5EFCF4409111FB22C15FF73717632EC3*, const RuntimeMethod*))Component_GetComponent_TisRuntimeObject_m7181F81CAEC2CF53F5D2BC79B7425C16E1F80D33_gshared)(__this, method);
}
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Object_op_Equality_mB6120F782D83091EF56A198FCEBCF066DB4A9605 (Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C* ___0_x, Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C* ___1_y, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Debug_Log_m87A9A3C761FF5C43ED8A53B16190A53D08F818BB (RuntimeObject* ___0_message, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Hexasphere_SetTilesConfigurationData_mE09BDF40221201C6DE3C320634DB5A3412E22FCF (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* __this, String_t* ___0_json, const RuntimeMethod* method) ;
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void MonoBehaviour__ctor_m592DB0105CA0BC97AA1C5F4AD27B12D68A3B7C1E (MonoBehaviour_t532A11E69716D348D8AA7F854AFCBFCB8AD17F71* __this, const RuntimeMethod* method) ;
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR float Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline (float ___0_value, const RuntimeMethod* method) ;
inline void List_1_AddWithResize_m79A9BF770BEF9C06BE40D5401E55E375F2726CC4 (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, RuntimeObject* ___0_item, const RuntimeMethod* method)
{
	((  void (*) (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D*, RuntimeObject*, const RuntimeMethod*))List_1_AddWithResize_m79A9BF770BEF9C06BE40D5401E55E375F2726CC4_gshared)(__this, ___0_item, method);
}
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Array_Clear_m50BAA3751899858B097D3FF2ED31F284703FE5CB (RuntimeArray* ___0_array, int32_t ___1_index, int32_t ___2_length, const RuntimeMethod* method) ;
inline void List_1_AddWithResize_m378B392086AAB6F400944FA9839516326B3F7BB8 (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method)
{
	((  void (*) (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*, int32_t, const RuntimeMethod*))List_1_AddWithResize_m378B392086AAB6F400944FA9839516326B3F7BB8_gshared)(__this, ___0_item, method);
}
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115047
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29 UnitySourceGeneratedAssemblyMonoScriptTypes_v1_Get_m225E8EE4053CDF8DC4138DBFC17C68903D288220 (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____603FC505D8667A39DDA6F8B988C09A307B128A8459995AA82761F35797FC50FF_FieldInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____F02AE59C30131A8E71EF5AD251B5E2D9B48B6D031F267523C2642A177C734295_FieldInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29 V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		il2cpp_codegen_initobj((&V_0), sizeof(MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29));
		ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* L_0 = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)SZArrayNew(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031_il2cpp_TypeInfo_var, (uint32_t)((int32_t)1061));
		ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* L_1 = L_0;
		RuntimeFieldHandle_t6E4C45B6D2EA12FC99185805A7E77527899B25C5 L_2 = { reinterpret_cast<intptr_t> (U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____603FC505D8667A39DDA6F8B988C09A307B128A8459995AA82761F35797FC50FF_FieldInfo_var) };
		RuntimeHelpers_InitializeArray_m751372AA3F24FBF6DA9B9D687CBFA2DE436CAB9B((RuntimeArray*)L_1, L_2, NULL);
		(&V_0)->___FilePathsData = L_1;
		Il2CppCodeGenWriteBarrier((void**)(&(&V_0)->___FilePathsData), (void*)L_1);
		ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* L_3 = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)SZArrayNew(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031_il2cpp_TypeInfo_var, (uint32_t)((int32_t)664));
		ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031* L_4 = L_3;
		RuntimeFieldHandle_t6E4C45B6D2EA12FC99185805A7E77527899B25C5 L_5 = { reinterpret_cast<intptr_t> (U3CPrivateImplementationDetailsU3E_t915E05343BAE8A0B564FB469AA70603F93E4158D____F02AE59C30131A8E71EF5AD251B5E2D9B48B6D031F267523C2642A177C734295_FieldInfo_var) };
		RuntimeHelpers_InitializeArray_m751372AA3F24FBF6DA9B9D687CBFA2DE436CAB9B((RuntimeArray*)L_4, L_5, NULL);
		(&V_0)->___TypesData = L_4;
		Il2CppCodeGenWriteBarrier((void**)(&(&V_0)->___TypesData), (void*)L_4);
		(&V_0)->___TotalFiles = ((int32_t)17);
		(&V_0)->___TotalTypes = ((int32_t)22);
		(&V_0)->___IsEditorOnly = (bool)0;
		MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29 L_6 = V_0;
		return L_6;
	}
}
// Method Definition Index: 115048
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void UnitySourceGeneratedAssemblyMonoScriptTypes_v1__ctor_m7B9370EC7625D5F5D901A5B8C0A73BD410BE7DFA (UnitySourceGeneratedAssemblyMonoScriptTypes_v1_t3ECE4AEC156020A8212642B1F162C353BE9ABEEB* __this, const RuntimeMethod* method) 
{
	{
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_pinvoke(const MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29& unmarshaled, MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_pinvoke& marshaled)
{
	marshaled.___FilePathsData = il2cpp_codegen_com_marshal_safe_array(IL2CPP_VT_I1, unmarshaled.___FilePathsData);
	marshaled.___TypesData = il2cpp_codegen_com_marshal_safe_array(IL2CPP_VT_I1, unmarshaled.___TypesData);
	marshaled.___TotalTypes = unmarshaled.___TotalTypes;
	marshaled.___TotalFiles = unmarshaled.___TotalFiles;
	marshaled.___IsEditorOnly = static_cast<int32_t>(unmarshaled.___IsEditorOnly);
}
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_pinvoke_back(const MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_pinvoke& marshaled, MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29& unmarshaled)
{
	unmarshaled.___FilePathsData = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___FilePathsData);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___FilePathsData), (void*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___FilePathsData));
	unmarshaled.___TypesData = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___TypesData);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___TypesData), (void*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___TypesData));
	int32_t unmarshaledTotalTypes_temp_2 = 0;
	unmarshaledTotalTypes_temp_2 = marshaled.___TotalTypes;
	unmarshaled.___TotalTypes = unmarshaledTotalTypes_temp_2;
	int32_t unmarshaledTotalFiles_temp_3 = 0;
	unmarshaledTotalFiles_temp_3 = marshaled.___TotalFiles;
	unmarshaled.___TotalFiles = unmarshaledTotalFiles_temp_3;
	bool unmarshaledIsEditorOnly_temp_4 = false;
	unmarshaledIsEditorOnly_temp_4 = static_cast<bool>(marshaled.___IsEditorOnly);
	unmarshaled.___IsEditorOnly = unmarshaledIsEditorOnly_temp_4;
}
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_pinvoke_cleanup(MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_pinvoke& marshaled)
{
	il2cpp_codegen_com_destroy_safe_array(marshaled.___FilePathsData);
	marshaled.___FilePathsData = NULL;
	il2cpp_codegen_com_destroy_safe_array(marshaled.___TypesData);
	marshaled.___TypesData = NULL;
}
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_com(const MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29& unmarshaled, MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_com& marshaled)
{
	marshaled.___FilePathsData = il2cpp_codegen_com_marshal_safe_array(IL2CPP_VT_I1, unmarshaled.___FilePathsData);
	marshaled.___TypesData = il2cpp_codegen_com_marshal_safe_array(IL2CPP_VT_I1, unmarshaled.___TypesData);
	marshaled.___TotalTypes = unmarshaled.___TotalTypes;
	marshaled.___TotalFiles = unmarshaled.___TotalFiles;
	marshaled.___IsEditorOnly = static_cast<int32_t>(unmarshaled.___IsEditorOnly);
}
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_com_back(const MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_com& marshaled, MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29& unmarshaled)
{
	unmarshaled.___FilePathsData = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___FilePathsData);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___FilePathsData), (void*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___FilePathsData));
	unmarshaled.___TypesData = (ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___TypesData);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___TypesData), (void*)(ByteU5BU5D_tA6237BF417AE52AD70CFB4EF24A7A82613DF9031*)il2cpp_codegen_com_marshal_safe_array_result(IL2CPP_VT_I1, il2cpp_defaults.byte_class, marshaled.___TypesData));
	int32_t unmarshaledTotalTypes_temp_2 = 0;
	unmarshaledTotalTypes_temp_2 = marshaled.___TotalTypes;
	unmarshaled.___TotalTypes = unmarshaledTotalTypes_temp_2;
	int32_t unmarshaledTotalFiles_temp_3 = 0;
	unmarshaledTotalFiles_temp_3 = marshaled.___TotalFiles;
	unmarshaled.___TotalFiles = unmarshaledTotalFiles_temp_3;
	bool unmarshaledIsEditorOnly_temp_4 = false;
	unmarshaledIsEditorOnly_temp_4 = static_cast<bool>(marshaled.___IsEditorOnly);
	unmarshaled.___IsEditorOnly = unmarshaledIsEditorOnly_temp_4;
}
IL2CPP_EXTERN_C void MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshal_com_cleanup(MonoScriptData_tF035534332FAC6AD6F81B9B1AB9B9130011C0C29_marshaled_com& marshaled)
{
	il2cpp_codegen_com_destroy_safe_array(marshaled.___FilePathsData);
	marshaled.___FilePathsData = NULL;
	il2cpp_codegen_com_destroy_safe_array(marshaled.___TypesData);
	marshaled.___TypesData = NULL;
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_Multicast(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	il2cpp_array_size_t length = __this->___delegates->max_length;
	Delegate_t** delegatesToInvoke = reinterpret_cast<Delegate_t**>(__this->___delegates->GetAddressAtUnchecked(0));
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* retVal = NULL;
	for (il2cpp_array_size_t i = 0; i < length; i++)
	{
		GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* currentDelegate = reinterpret_cast<GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206*>(delegatesToInvoke[i]);
		typedef Point_t13126743CEDB2A83E25B6018553E5022E06D2790* (*FunctionPointerType) (RuntimeObject*, Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*);
		retVal = ((FunctionPointerType)currentDelegate->___invoke_impl)((Il2CppObject*)currentDelegate->___method_code, ___0_point, reinterpret_cast<RuntimeMethod*>(currentDelegate->___method));
	}
	return retVal;
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenInst(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	NullCheck(___0_point);
	typedef Point_t13126743CEDB2A83E25B6018553E5022E06D2790* (*FunctionPointerType) (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___method_ptr)(___0_point, method);
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenStatic(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	typedef Point_t13126743CEDB2A83E25B6018553E5022E06D2790* (*FunctionPointerType) (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___method_ptr)(___0_point, method);
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenVirtual(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	NullCheck(___0_point);
	return VirtualFuncInvoker0< Point_t13126743CEDB2A83E25B6018553E5022E06D2790* >::Invoke(il2cpp_codegen_method_get_slot(method), ___0_point);
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenInterface(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	NullCheck(___0_point);
	return InterfaceFuncInvoker0< Point_t13126743CEDB2A83E25B6018553E5022E06D2790* >::Invoke(il2cpp_codegen_method_get_slot(method), il2cpp_codegen_method_get_declaring_type(method), ___0_point);
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenGenericVirtual(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	NullCheck(___0_point);
	return GenericVirtualFuncInvoker0< Point_t13126743CEDB2A83E25B6018553E5022E06D2790* >::Invoke(method, ___0_point);
}
Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenGenericInterface(GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method)
{
	NullCheck(___0_point);
	return GenericInterfaceFuncInvoker0< Point_t13126743CEDB2A83E25B6018553E5022E06D2790* >::Invoke(method, ___0_point);
}
// Method Definition Index: 115049
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void GetCachedPointDelegate__ctor_m3A92F9A9390EA49BE379888F45E1007E24379683 (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, RuntimeObject* ___0_object, intptr_t ___1_method, const RuntimeMethod* method) 
{
	__this->___method_ptr = (intptr_t)il2cpp_codegen_get_method_pointer((RuntimeMethod*)___1_method);
	__this->___method = ___1_method;
	__this->___m_target = ___0_object;
	Il2CppCodeGenWriteBarrier((void**)(&__this->___m_target), (void*)___0_object);
	int parameterCount = il2cpp_codegen_method_parameter_count((RuntimeMethod*)___1_method);
	__this->___method_code = (intptr_t)__this;
	if (MethodIsStatic((RuntimeMethod*)___1_method))
	{
		bool isOpen = parameterCount == 1;
		if (isOpen)
			__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenStatic;
		else
			{
				__this->___invoke_impl = __this->___method_ptr;
				__this->___method_code = (intptr_t)__this->___m_target;
			}
	}
	else
	{
		bool isOpen = parameterCount == 0;
		if (isOpen)
		{
			if (__this->___method_is_virtual)
			{
				if (il2cpp_codegen_method_is_generic_instance_method((RuntimeMethod*)___1_method))
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenGenericInterface;
					else
						__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenGenericVirtual;
				else
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenInterface;
					else
						__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenVirtual;
			}
			else
			{
				__this->___invoke_impl = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_OpenInst;
			}
		}
		else
		{
			if (___0_object == NULL)
				il2cpp_codegen_raise_exception(il2cpp_codegen_get_argument_exception(NULL, "Delegate to an instance method cannot have null 'this'."), NULL);
			__this->___invoke_impl = __this->___method_ptr;
			__this->___method_code = (intptr_t)__this->___m_target;
		}
	}
	__this->___extra_arg = (intptr_t)&GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_Multicast;
}
// Method Definition Index: 115050
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method) 
{
	typedef Point_t13126743CEDB2A83E25B6018553E5022E06D2790* (*FunctionPointerType) (RuntimeObject*, Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___invoke_impl)((Il2CppObject*)__this->___method_code, ___0_point, reinterpret_cast<RuntimeMethod*>(__this->___method));
}
// Method Definition Index: 115051
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* GetCachedPointDelegate_BeginInvoke_mBA13172C9B43D53FB2594CBA660E89ED2E2F9DDA (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C* ___1_callback, RuntimeObject* ___2_object, const RuntimeMethod* method) 
{
	void *__d_args[2] = {0};
	__d_args[0] = ___0_point;
	return (RuntimeObject*)il2cpp_codegen_delegate_begin_invoke((RuntimeDelegate*)__this, __d_args, (RuntimeDelegate*)___1_callback, (RuntimeObject*)___2_object);
}
// Method Definition Index: 115052
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_EndInvoke_m20479405B147E7F4196F58AC8CEE39FBCB6CDDD9 (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, RuntimeObject* ___0_result, const RuntimeMethod* method) 
{
	RuntimeObject *__result = il2cpp_codegen_delegate_end_invoke((Il2CppAsyncResult*) ___0_result, 0);
	return (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)__result;
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_Multicast(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	il2cpp_array_size_t length = __this->___delegates->max_length;
	Delegate_t** delegatesToInvoke = reinterpret_cast<Delegate_t**>(__this->___delegates->GetAddressAtUnchecked(0));
	float retVal = 0.0f;
	for (il2cpp_array_size_t i = 0; i < length; i++)
	{
		PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* currentDelegate = reinterpret_cast<PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9*>(delegatesToInvoke[i]);
		typedef float (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, int32_t, const RuntimeMethod*);
		retVal = ((FunctionPointerType)currentDelegate->___invoke_impl)((Il2CppObject*)currentDelegate->___method_code, ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex, reinterpret_cast<RuntimeMethod*>(currentDelegate->___method));
	}
	return retVal;
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenInst(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	typedef float (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, int32_t, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex, method);
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenStatic(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	typedef float (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, int32_t, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex, method);
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenVirtual(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	return VirtualFuncInvoker2< float, int32_t, int32_t >::Invoke(il2cpp_codegen_method_get_slot(method), ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex);
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenInterface(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	return InterfaceFuncInvoker2< float, int32_t, int32_t >::Invoke(il2cpp_codegen_method_get_slot(method), il2cpp_codegen_method_get_declaring_type(method), ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex);
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenGenericVirtual(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	return GenericVirtualFuncInvoker2< float, int32_t, int32_t >::Invoke(method, ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex);
}
float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenGenericInterface(PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	return GenericInterfaceFuncInvoker2< float, int32_t, int32_t >::Invoke(method, ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex);
}
// Method Definition Index: 115333
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PathFindingEvent__ctor_m7DB287173B2C5B2470B78C1699A5EB31881152EA (PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, RuntimeObject* ___0_object, intptr_t ___1_method, const RuntimeMethod* method) 
{
	__this->___method_ptr = (intptr_t)il2cpp_codegen_get_method_pointer((RuntimeMethod*)___1_method);
	__this->___method = ___1_method;
	__this->___m_target = ___0_object;
	Il2CppCodeGenWriteBarrier((void**)(&__this->___m_target), (void*)___0_object);
	int parameterCount = il2cpp_codegen_method_parameter_count((RuntimeMethod*)___1_method);
	__this->___method_code = (intptr_t)__this;
	if (MethodIsStatic((RuntimeMethod*)___1_method))
	{
		bool isOpen = parameterCount == 3;
		if (isOpen)
			__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenStatic;
		else
			{
				__this->___invoke_impl = __this->___method_ptr;
				__this->___method_code = (intptr_t)__this->___m_target;
			}
	}
	else
	{
		bool isOpen = parameterCount == 2;
		if (isOpen)
		{
			if (__this->___method_is_virtual)
			{
				if (il2cpp_codegen_method_is_generic_instance_method((RuntimeMethod*)___1_method))
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenGenericInterface;
					else
						__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenGenericVirtual;
				else
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenInterface;
					else
						__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenVirtual;
			}
			else
			{
				__this->___invoke_impl = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_OpenInst;
			}
		}
		else
		{
			if (___0_object == NULL)
				il2cpp_codegen_raise_exception(il2cpp_codegen_get_argument_exception(NULL, "Delegate to an instance method cannot have null 'this'."), NULL);
			__this->___invoke_impl = __this->___method_ptr;
			__this->___method_code = (intptr_t)__this->___m_target;
		}
	}
	__this->___extra_arg = (intptr_t)&PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC_Multicast;
}
// Method Definition Index: 115334
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float PathFindingEvent_Invoke_mB959EFD37CC401AA4F64114AF178B9550FB071CC (PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, const RuntimeMethod* method) 
{
	typedef float (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, int32_t, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___invoke_impl)((Il2CppObject*)__this->___method_code, ___0_hexasphere, ___1_toTileIndex, ___2_fromTileIndex, reinterpret_cast<RuntimeMethod*>(__this->___method));
}
// Method Definition Index: 115335
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* PathFindingEvent_BeginInvoke_mA32007BE937DF2489649FDAA7886FBDEAFC848D9 (PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_toTileIndex, int32_t ___2_fromTileIndex, AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C* ___3_callback, RuntimeObject* ___4_object, const RuntimeMethod* method) 
{
	void *__d_args[4] = {0};
	__d_args[0] = ___0_hexasphere;
	__d_args[1] = Box(il2cpp_defaults.int32_class, &___1_toTileIndex);
	__d_args[2] = Box(il2cpp_defaults.int32_class, &___2_fromTileIndex);
	return (RuntimeObject*)il2cpp_codegen_delegate_begin_invoke((RuntimeDelegate*)__this, __d_args, (RuntimeDelegate*)___3_callback, (RuntimeObject*)___4_object);
}
// Method Definition Index: 115336
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float PathFindingEvent_EndInvoke_mDD6BDE8C938CF2B17DDEF1CFBD2A3140B61CB4A4 (PathFindingEvent_tAC1B357C66C743FCB3ECD82365EC9B9115F4C5B9* __this, RuntimeObject* ___0_result, const RuntimeMethod* method) 
{
	RuntimeObject *__result = il2cpp_codegen_delegate_end_invoke((Il2CppAsyncResult*) ___0_result, 0);
	return *(float*)UnBox ((RuntimeObject*)__result);
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_Multicast(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	il2cpp_array_size_t length = __this->___delegates->max_length;
	Delegate_t** delegatesToInvoke = reinterpret_cast<Delegate_t**>(__this->___delegates->GetAddressAtUnchecked(0));
	for (il2cpp_array_size_t i = 0; i < length; i++)
	{
		TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* currentDelegate = reinterpret_cast<TileEvent_t3392B77898A6708FA7D695CF027BB60332242782*>(delegatesToInvoke[i]);
		typedef void (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, const RuntimeMethod*);
		((FunctionPointerType)currentDelegate->___invoke_impl)((Il2CppObject*)currentDelegate->___method_code, ___0_hexasphere, ___1_tileIndex, reinterpret_cast<RuntimeMethod*>(currentDelegate->___method));
	}
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenInst(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	typedef void (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, const RuntimeMethod*);
	((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, ___1_tileIndex, method);
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenStatic(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	typedef void (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, const RuntimeMethod*);
	((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, ___1_tileIndex, method);
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenVirtual(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	VirtualActionInvoker1< int32_t >::Invoke(il2cpp_codegen_method_get_slot(method), ___0_hexasphere, ___1_tileIndex);
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenInterface(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	InterfaceActionInvoker1< int32_t >::Invoke(il2cpp_codegen_method_get_slot(method), il2cpp_codegen_method_get_declaring_type(method), ___0_hexasphere, ___1_tileIndex);
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenGenericVirtual(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	GenericVirtualActionInvoker1< int32_t >::Invoke(method, ___0_hexasphere, ___1_tileIndex);
}
void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenGenericInterface(TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	GenericInterfaceActionInvoker1< int32_t >::Invoke(method, ___0_hexasphere, ___1_tileIndex);
}
// Method Definition Index: 115337
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TileEvent__ctor_mDC8E605DD8506F3DC6B0981BD02F34754D09E89F (TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, RuntimeObject* ___0_object, intptr_t ___1_method, const RuntimeMethod* method) 
{
	__this->___method_ptr = (intptr_t)il2cpp_codegen_get_method_pointer((RuntimeMethod*)___1_method);
	__this->___method = ___1_method;
	__this->___m_target = ___0_object;
	Il2CppCodeGenWriteBarrier((void**)(&__this->___m_target), (void*)___0_object);
	int parameterCount = il2cpp_codegen_method_parameter_count((RuntimeMethod*)___1_method);
	__this->___method_code = (intptr_t)__this;
	if (MethodIsStatic((RuntimeMethod*)___1_method))
	{
		bool isOpen = parameterCount == 2;
		if (isOpen)
			__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenStatic;
		else
			{
				__this->___invoke_impl = __this->___method_ptr;
				__this->___method_code = (intptr_t)__this->___m_target;
			}
	}
	else
	{
		bool isOpen = parameterCount == 1;
		if (isOpen)
		{
			if (__this->___method_is_virtual)
			{
				if (il2cpp_codegen_method_is_generic_instance_method((RuntimeMethod*)___1_method))
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenGenericInterface;
					else
						__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenGenericVirtual;
				else
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenInterface;
					else
						__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenVirtual;
			}
			else
			{
				__this->___invoke_impl = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_OpenInst;
			}
		}
		else
		{
			if (___0_object == NULL)
				il2cpp_codegen_raise_exception(il2cpp_codegen_get_argument_exception(NULL, "Delegate to an instance method cannot have null 'this'."), NULL);
			__this->___invoke_impl = __this->___method_ptr;
			__this->___method_code = (intptr_t)__this->___m_target;
		}
	}
	__this->___extra_arg = (intptr_t)&TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588_Multicast;
}
// Method Definition Index: 115338
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TileEvent_Invoke_m0557A07A247CA8FA808659FE2A47F7EEA40BE588 (TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, const RuntimeMethod* method) 
{
	typedef void (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, int32_t, const RuntimeMethod*);
	((FunctionPointerType)__this->___invoke_impl)((Il2CppObject*)__this->___method_code, ___0_hexasphere, ___1_tileIndex, reinterpret_cast<RuntimeMethod*>(__this->___method));
}
// Method Definition Index: 115339
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* TileEvent_BeginInvoke_mCBE172AE94CB9C6EEF970584FD0E965CF4DC1F68 (TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, int32_t ___1_tileIndex, AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C* ___2_callback, RuntimeObject* ___3_object, const RuntimeMethod* method) 
{
	void *__d_args[3] = {0};
	__d_args[0] = ___0_hexasphere;
	__d_args[1] = Box(il2cpp_defaults.int32_class, &___1_tileIndex);
	return (RuntimeObject*)il2cpp_codegen_delegate_begin_invoke((RuntimeDelegate*)__this, __d_args, (RuntimeDelegate*)___2_callback, (RuntimeObject*)___3_object);
}
// Method Definition Index: 115340
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TileEvent_EndInvoke_m8CB52BB14BFFB326D6B9A046F64D4798E7A6F2F7 (TileEvent_t3392B77898A6708FA7D695CF027BB60332242782* __this, RuntimeObject* ___0_result, const RuntimeMethod* method) 
{
	il2cpp_codegen_delegate_end_invoke((Il2CppAsyncResult*) ___0_result, 0);
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_Multicast(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	il2cpp_array_size_t length = __this->___delegates->max_length;
	Delegate_t** delegatesToInvoke = reinterpret_cast<Delegate_t**>(__this->___delegates->GetAddressAtUnchecked(0));
	for (il2cpp_array_size_t i = 0; i < length; i++)
	{
		HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* currentDelegate = reinterpret_cast<HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF*>(delegatesToInvoke[i]);
		typedef void (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, const RuntimeMethod*);
		((FunctionPointerType)currentDelegate->___invoke_impl)((Il2CppObject*)currentDelegate->___method_code, ___0_hexasphere, reinterpret_cast<RuntimeMethod*>(currentDelegate->___method));
	}
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenInst(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	typedef void (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, const RuntimeMethod*);
	((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, method);
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenStatic(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	typedef void (*FunctionPointerType) (Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, const RuntimeMethod*);
	((FunctionPointerType)__this->___method_ptr)(___0_hexasphere, method);
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenVirtual(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	VirtualActionInvoker0::Invoke(il2cpp_codegen_method_get_slot(method), ___0_hexasphere);
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenInterface(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	InterfaceActionInvoker0::Invoke(il2cpp_codegen_method_get_slot(method), il2cpp_codegen_method_get_declaring_type(method), ___0_hexasphere);
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenGenericVirtual(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	GenericVirtualActionInvoker0::Invoke(method, ___0_hexasphere);
}
void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenGenericInterface(HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method)
{
	NullCheck(___0_hexasphere);
	GenericInterfaceActionInvoker0::Invoke(method, ___0_hexasphere);
}
// Method Definition Index: 115341
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereEvent__ctor_m786B53EDAA755A7B15721EDDA4751858D2EA56D1 (HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, RuntimeObject* ___0_object, intptr_t ___1_method, const RuntimeMethod* method) 
{
	__this->___method_ptr = (intptr_t)il2cpp_codegen_get_method_pointer((RuntimeMethod*)___1_method);
	__this->___method = ___1_method;
	__this->___m_target = ___0_object;
	Il2CppCodeGenWriteBarrier((void**)(&__this->___m_target), (void*)___0_object);
	int parameterCount = il2cpp_codegen_method_parameter_count((RuntimeMethod*)___1_method);
	__this->___method_code = (intptr_t)__this;
	if (MethodIsStatic((RuntimeMethod*)___1_method))
	{
		bool isOpen = parameterCount == 1;
		if (isOpen)
			__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenStatic;
		else
			{
				__this->___invoke_impl = __this->___method_ptr;
				__this->___method_code = (intptr_t)__this->___m_target;
			}
	}
	else
	{
		bool isOpen = parameterCount == 0;
		if (isOpen)
		{
			if (__this->___method_is_virtual)
			{
				if (il2cpp_codegen_method_is_generic_instance_method((RuntimeMethod*)___1_method))
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenGenericInterface;
					else
						__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenGenericVirtual;
				else
					if (il2cpp_codegen_method_is_interface_method((RuntimeMethod*)___1_method))
						__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenInterface;
					else
						__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenVirtual;
			}
			else
			{
				__this->___invoke_impl = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_OpenInst;
			}
		}
		else
		{
			if (___0_object == NULL)
				il2cpp_codegen_raise_exception(il2cpp_codegen_get_argument_exception(NULL, "Delegate to an instance method cannot have null 'this'."), NULL);
			__this->___invoke_impl = __this->___method_ptr;
			__this->___method_code = (intptr_t)__this->___m_target;
		}
	}
	__this->___extra_arg = (intptr_t)&HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985_Multicast;
}
// Method Definition Index: 115342
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereEvent_Invoke_m722E5778C0F1DED674D8D1494EEEAC58EA7E7985 (HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, const RuntimeMethod* method) 
{
	typedef void (*FunctionPointerType) (RuntimeObject*, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC*, const RuntimeMethod*);
	((FunctionPointerType)__this->___invoke_impl)((Il2CppObject*)__this->___method_code, ___0_hexasphere, reinterpret_cast<RuntimeMethod*>(__this->___method));
}
// Method Definition Index: 115343
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* HexasphereEvent_BeginInvoke_m2F4FE6633D1DC6F3E2829AF4D1EC0B2D5FB94866 (HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* ___0_hexasphere, AsyncCallback_t7FEF460CBDCFB9C5FA2EF776984778B9A4145F4C* ___1_callback, RuntimeObject* ___2_object, const RuntimeMethod* method) 
{
	void *__d_args[2] = {0};
	__d_args[0] = ___0_hexasphere;
	return (RuntimeObject*)il2cpp_codegen_delegate_begin_invoke((RuntimeDelegate*)__this, __d_args, (RuntimeDelegate*)___1_callback, (RuntimeObject*)___2_object);
}
// Method Definition Index: 115344
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereEvent_EndInvoke_m0711A14FBA07C4C663FED590A190CCCDCC8B59DD (HexasphereEvent_tB5F22BB1BD67A07D3E87AE6A08A3679A68328BEF* __this, RuntimeObject* ___0_result, const RuntimeMethod* method) 
{
	il2cpp_codegen_delegate_end_invoke((Il2CppAsyncResult*) ___0_result, 0);
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115345
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PFNodesComparer__ctor_m54C19B3C81EF381A22A8C54C5F2634EC145B2136 (PFNodesComparer_t2721E614A3AB471BBCC4CB6CDB3E9CEB9071B513* __this, PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* ___0_nodes, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:185>
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:186>
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_0 = ___0_nodes;
		__this->___m = L_0;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___m), (void*)L_0);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:187>
		return;
	}
}
// Method Definition Index: 115346
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t PFNodesComparer_Compare_m9D041CFB0BCED5BAE54B7442B96E34BD361DF057 (PFNodesComparer_t2721E614A3AB471BBCC4CB6CDB3E9CEB9071B513* __this, int32_t ___0_a, int32_t ___1_b, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:190>
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_0 = __this->___m;
		int32_t L_1 = ___0_a;
		NullCheck(L_0);
		float L_2 = ((L_0)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_1)))->___f;
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_3 = __this->___m;
		int32_t L_4 = ___1_b;
		NullCheck(L_3);
		float L_5 = ((L_3)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_4)))->___f;
		if ((!(((float)L_2) > ((float)L_5))))
		{
			goto IL_0026;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:191>
		return 1;
	}

IL_0026:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:192>
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_6 = __this->___m;
		int32_t L_7 = ___0_a;
		NullCheck(L_6);
		float L_8 = ((L_6)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_7)))->___f;
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_9 = __this->___m;
		int32_t L_10 = ___1_b;
		NullCheck(L_9);
		float L_11 = ((L_9)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_10)))->___f;
		if ((!(((float)L_8) < ((float)L_11))))
		{
			goto IL_004c;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:193>
		return (-1);
	}

IL_004c:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:194>
		return 0;
	}
}
// Method Definition Index: 115347
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PFNodesComparer_SetMatrix_mB43915CC5AA9450BC6B86EEC812101AA9E6BD523 (PFNodesComparer_t2721E614A3AB471BBCC4CB6CDB3E9CEB9071B513* __this, PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* ___0_nodes, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:198>
		PFNodeFastU5BU5D_t97D62CE050F1335343151D07AAAD79AB3490A73B* L_0 = ___0_nodes;
		__this->___m = L_0;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___m), (void*)L_0);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:199>
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115348
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR RuntimeObject* PQInt_get_comparer_mBCDEA863AA8A734DA67502DD210DA9D626333C63 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:207>
		RuntimeObject* L_0 = __this->___mComparer;
		return L_0;
	}
}
// Method Definition Index: 115349
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PQInt__ctor_mD140FC983FA81B698EDC51DA427527D05DDC509F (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, RuntimeObject* ___0_comparer, int32_t ___1_capacity, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:209>
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:210>
		RuntimeObject* L_0 = ___0_comparer;
		__this->___mComparer = L_0;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___mComparer), (void*)L_0);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:211>
		int32_t L_1 = ___1_capacity;
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_2 = (Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C*)(Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C*)SZArrayNew(Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C_il2cpp_TypeInfo_var, (uint32_t)L_1);
		__this->___tiles = L_2;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___tiles), (void*)L_2);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:212>
		__this->___tilesCount = 0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:213>
		return;
	}
}
// Method Definition Index: 115350
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PQInt_Swap_m567F5C190448FABE06702552EB2885D7556020E9 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, int32_t ___0_i, int32_t ___1_j, const RuntimeMethod* method) 
{
	int32_t V_0 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:216>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_0 = __this->___tiles;
		int32_t L_1 = ___0_i;
		NullCheck(L_0);
		int32_t L_2 = L_1;
		int32_t L_3 = (L_0)->GetAt(static_cast<il2cpp_array_size_t>(L_2));
		V_0 = L_3;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:217>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_4 = __this->___tiles;
		int32_t L_5 = ___0_i;
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_6 = __this->___tiles;
		int32_t L_7 = ___1_j;
		NullCheck(L_6);
		int32_t L_8 = L_7;
		int32_t L_9 = (L_6)->GetAt(static_cast<il2cpp_array_size_t>(L_8));
		NullCheck(L_4);
		(L_4)->SetAt(static_cast<il2cpp_array_size_t>(L_5), (int32_t)L_9);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:218>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_10 = __this->___tiles;
		int32_t L_11 = ___1_j;
		int32_t L_12 = V_0;
		NullCheck(L_10);
		(L_10)->SetAt(static_cast<il2cpp_array_size_t>(L_11), (int32_t)L_12);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:219>
		return;
	}
}
// Method Definition Index: 115351
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t PQInt_Compare_mBF5FC828D5E4D31492590E496A610F5070E972D0 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, int32_t ___0_i, int32_t ___1_j, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&IComparer_1_t4483F9B9F43C7B0F8D4FEEAE12FAFDD3F9CF81FD_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:222>
		RuntimeObject* L_0 = __this->___mComparer;
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_1 = __this->___tiles;
		int32_t L_2 = ___0_i;
		NullCheck(L_1);
		int32_t L_3 = L_2;
		int32_t L_4 = (L_1)->GetAt(static_cast<il2cpp_array_size_t>(L_3));
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_5 = __this->___tiles;
		int32_t L_6 = ___1_j;
		NullCheck(L_5);
		int32_t L_7 = L_6;
		int32_t L_8 = (L_5)->GetAt(static_cast<il2cpp_array_size_t>(L_7));
		NullCheck(L_0);
		int32_t L_9;
		L_9 = InterfaceFuncInvoker2< int32_t, int32_t, int32_t >::Invoke(0, IComparer_1_t4483F9B9F43C7B0F8D4FEEAE12FAFDD3F9CF81FD_il2cpp_TypeInfo_var, L_0, L_4, L_8);
		return L_9;
	}
}
// Method Definition Index: 115352
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t PQInt_Pop_mF8C222C4AA16025B657F82A9892326D47C358B21 (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, const RuntimeMethod* method) 
{
	int32_t V_0 = 0;
	int32_t V_1 = 0;
	int32_t V_2 = 0;
	int32_t V_3 = 0;
	int32_t V_4 = 0;
	int32_t V_5 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:226>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_0 = __this->___tiles;
		NullCheck(L_0);
		int32_t L_1 = 0;
		int32_t L_2 = (L_0)->GetAt(static_cast<il2cpp_array_size_t>(L_1));
		V_0 = L_2;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:227>
		V_1 = 0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:228>
		int32_t L_3 = __this->___tilesCount;
		V_5 = ((int32_t)il2cpp_codegen_subtract(L_3, 1));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:229>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_4 = __this->___tiles;
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_5 = __this->___tiles;
		int32_t L_6 = V_5;
		NullCheck(L_5);
		int32_t L_7 = L_6;
		int32_t L_8 = (L_5)->GetAt(static_cast<il2cpp_array_size_t>(L_7));
		NullCheck(L_4);
		(L_4)->SetAt(static_cast<il2cpp_array_size_t>(0), (int32_t)L_8);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:230>
		int32_t L_9 = __this->___tilesCount;
		__this->___tilesCount = ((int32_t)il2cpp_codegen_subtract(L_9, 1));
	}

IL_0034:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:232>
		int32_t L_10 = V_1;
		V_4 = L_10;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:233>
		int32_t L_11 = V_1;
		V_2 = ((int32_t)il2cpp_codegen_add(((int32_t)il2cpp_codegen_multiply(2, L_11)), 1));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:234>
		int32_t L_12 = V_2;
		V_3 = ((int32_t)il2cpp_codegen_add(L_12, 1));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:235>
		int32_t L_13 = V_5;
		int32_t L_14 = V_2;
		if ((((int32_t)L_13) <= ((int32_t)L_14)))
		{
			goto IL_0053;
		}
	}
	{
		int32_t L_15 = V_1;
		int32_t L_16 = V_2;
		int32_t L_17;
		L_17 = PQInt_Compare_mBF5FC828D5E4D31492590E496A610F5070E972D0(__this, L_15, L_16, NULL);
		if ((((int32_t)L_17) <= ((int32_t)0)))
		{
			goto IL_0053;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:236>
		int32_t L_18 = V_2;
		V_1 = L_18;
	}

IL_0053:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:237>
		int32_t L_19 = V_5;
		int32_t L_20 = V_3;
		if ((((int32_t)L_19) <= ((int32_t)L_20)))
		{
			goto IL_0065;
		}
	}
	{
		int32_t L_21 = V_1;
		int32_t L_22 = V_3;
		int32_t L_23;
		L_23 = PQInt_Compare_mBF5FC828D5E4D31492590E496A610F5070E972D0(__this, L_21, L_22, NULL);
		if ((((int32_t)L_23) <= ((int32_t)0)))
		{
			goto IL_0065;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:238>
		int32_t L_24 = V_3;
		V_1 = L_24;
	}

IL_0065:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:240>
		int32_t L_25 = V_1;
		int32_t L_26 = V_4;
		if ((((int32_t)L_25) == ((int32_t)L_26)))
		{
			goto IL_0075;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:242>
		int32_t L_27 = V_1;
		int32_t L_28 = V_4;
		PQInt_Swap_m567F5C190448FABE06702552EB2885D7556020E9(__this, L_27, L_28, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:243>
		goto IL_0034;
	}

IL_0075:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:245>
		int32_t L_29 = V_0;
		return L_29;
	}
}
// Method Definition Index: 115353
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t PQInt_Push_mFCD60A1D71FEE4429E8E34CDA3487C81A44228EC (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, int32_t ___0_item, const RuntimeMethod* method) 
{
	int32_t V_0 = 0;
	int32_t V_1 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:250>
		int32_t L_0 = __this->___tilesCount;
		V_0 = L_0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:251>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_1 = __this->___tiles;
		int32_t L_2 = __this->___tilesCount;
		int32_t L_3 = ___0_item;
		NullCheck(L_1);
		(L_1)->SetAt(static_cast<il2cpp_array_size_t>(L_2), (int32_t)L_3);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:252>
		int32_t L_4 = __this->___tilesCount;
		__this->___tilesCount = ((int32_t)il2cpp_codegen_add(L_4, 1));
	}

IL_0023:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:254>
		int32_t L_5 = V_0;
		if (!L_5)
		{
			goto IL_0043;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:256>
		int32_t L_6 = V_0;
		V_1 = ((int32_t)(((int32_t)il2cpp_codegen_subtract(L_6, 1))/2));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:257>
		int32_t L_7 = V_0;
		int32_t L_8 = V_1;
		int32_t L_9;
		L_9 = PQInt_Compare_mBF5FC828D5E4D31492590E496A610F5070E972D0(__this, L_7, L_8, NULL);
		if ((((int32_t)L_9) >= ((int32_t)0)))
		{
			goto IL_0043;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:258>
		int32_t L_10 = V_0;
		int32_t L_11 = V_1;
		PQInt_Swap_m567F5C190448FABE06702552EB2885D7556020E9(__this, L_10, L_11, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:259>
		int32_t L_12 = V_1;
		V_0 = L_12;
		goto IL_0023;
	}

IL_0043:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:263>
		int32_t L_13 = V_0;
		return L_13;
	}
}
// Method Definition Index: 115354
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void PQInt_Clear_mA6F540DBB4A3CD6E30D0F18F5BC8DD411367450A (PQInt_t06D2495D13CD3CAB3433C44A474096C801F53E38* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:267>
		__this->___tilesCount = 0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/HexaspherePrivPathFinder.cs:268>
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115355
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float Misc_Vector3SqrDistance_m02A226F4A48DE3AD13E31B9D99BD574E9FD7A870 (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_a, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_b, const RuntimeMethod* method) 
{
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:19>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ___0_a;
		float L_1 = L_0.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2 = ___1_b;
		float L_3 = L_2.___x;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:20>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4 = ___0_a;
		float L_5 = L_4.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6 = ___1_b;
		float L_7 = L_6.___y;
		V_0 = ((float)il2cpp_codegen_subtract(L_5, L_7));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:21>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_8 = ___0_a;
		float L_9 = L_8.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_10 = ___1_b;
		float L_11 = L_10.___z;
		V_1 = ((float)il2cpp_codegen_subtract(L_9, L_11));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:22>
		float L_12 = ((float)il2cpp_codegen_subtract(L_1, L_3));
		float L_13 = V_0;
		float L_14 = V_0;
		float L_15 = V_1;
		float L_16 = V_1;
		return ((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(((float)il2cpp_codegen_multiply(L_12, L_12)), ((float)il2cpp_codegen_multiply(L_13, L_14)))), ((float)il2cpp_codegen_multiply(L_15, L_16))));
	}
}
// Method Definition Index: 115356
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Misc__cctor_m7ED80A08C42EA9997BBE9AAC27D9D152259551AD (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:10>
		Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 L_0;
		L_0 = Vector4_get_zero_m3D61F5FA9483CD9C08977D9D8852FB448B4CE6D1_inline(NULL);
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_1;
		L_1 = Vector4_op_Implicit_m0217ADDC8CADDB93ACBABB17A50207698DAB0071_inline(L_0, NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector4zero = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:11>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2;
		L_2 = Vector3_get_one_mC9B289F1E15C42C597180C9FE6FB492495B51D02_inline(NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector3one = L_2;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:12>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_3;
		L_3 = Vector3_get_zero_m0C1249C3F25B1C70EAD3CC8B31259975A457AE39_inline(NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector3zero = L_3;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:13>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4;
		L_4 = Vector3_get_up_m128AF3FDC820BF59D5DE86D973E7DE3F20C3AEBA_inline(NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector3up = L_4;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:14>
		Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 L_5;
		L_5 = Vector2_get_one_m9097EB8DC23C26118A591AF16702796C3EF51DFB_inline(NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector2one = L_5;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:15>
		Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 L_6;
		L_6 = Vector2_get_zero_m32506C40EC2EE7D5D4410BF40D3EE683A3D5F32C_inline(NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Vector2zero = L_6;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Misc.cs:16>
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_7;
		L_7 = Color_get_white_m068F5AF879B0FCA584E3693F762EA41BB65532C6_inline(NULL);
		Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B L_8;
		L_8 = Color32_op_Implicit_m79AF5E0BDE9CE041CAC4D89CBFA66E71C6DD1B70_inline(L_7, NULL);
		((Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_StaticFields*)il2cpp_codegen_static_fields_for(Misc_tA596AAE116A1FB09DA30EF36D310DA2A23001779_il2cpp_TypeInfo_var))->___Color32White = L_8;
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115357
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float Point_get_elevation_mF64CE014A96AE0BCAAA84B185B676C02B695D5CC (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:15>
		float L_0 = __this->____elevation;
		return L_0;
	}
}
// Method Definition Index: 115358
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_set_elevation_mDF3B4AEAC3E76F85CDF5E960B899BCD487F15B32 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, float ___0_value, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:18>
		float L_0 = __this->____elevation;
		float L_1 = ___0_value;
		if ((((float)L_0) == ((float)L_1)))
		{
			goto IL_0017;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:19>
		float L_2 = ___0_value;
		__this->____elevation = L_2;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:20>
		__this->____projectedVector3Computed = (bool)0;
	}

IL_0017:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:22>
		return;
	}
}
// Method Definition Index: 115359
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Point_get_projectedVector3_m173ED0275B0A7F93BCE5B23F34BFA602C68F33D6 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:31>
		bool L_0 = __this->____projectedVector3Computed;
		if (!L_0)
		{
			goto IL_000f;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:32>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_1 = __this->____projectedVector3;
		return L_1;
	}

IL_000f:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:34>
		Point_ComputeProjectedVertex_m48B50AB8903161CF2286AB0D4DFBD74987AE50D3(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:35>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2 = __this->____projectedVector3;
		return L_2;
	}
}
// Method Definition Index: 115360
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, float ___0_x, float ___1_y, float ___2_z, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:46>
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:47>
		float L_0 = ___0_x;
		__this->___x = L_0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:48>
		float L_1 = ___1_y;
		__this->___y = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:49>
		float L_2 = ___2_z;
		__this->___z = L_2;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:50>
		return;
	}
}
// Method Definition Index: 115361
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* Point_Subdivide_mCBBC12A84C2C20140225A9C7773B85C88B87FC53 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, int32_t ___1_count, GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* ___2_checkPoint, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1__ctor_m04CF8E658DFD6C00F15510DD05E2CE000175075E_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* V_0 = NULL;
	double V_1 = 0.0;
	double V_2 = 0.0;
	double V_3 = 0.0;
	double V_4 = 0.0;
	double V_5 = 0.0;
	double V_6 = 0.0;
	double V_7 = 0.0;
	int32_t V_8 = 0;
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_9 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:53>
		int32_t L_0 = ___1_count;
		List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* L_1 = (List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F*)il2cpp_codegen_object_new(List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F_il2cpp_TypeInfo_var);
		List_1__ctor_m04CF8E658DFD6C00F15510DD05E2CE000175075E(L_1, ((int32_t)il2cpp_codegen_add(L_0, 1)), List_1__ctor_m04CF8E658DFD6C00F15510DD05E2CE000175075E_RuntimeMethod_var);
		V_0 = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:54>
		List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* L_2 = V_0;
		NullCheck(L_2);
		List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_inline(L_2, __this, List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:56>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_3 = ___0_point;
		NullCheck(L_3);
		float L_4 = L_3->___x;
		float L_5 = __this->___x;
		V_1 = ((double)((float)il2cpp_codegen_subtract(L_4, L_5)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:57>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___0_point;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		float L_8 = __this->___y;
		V_2 = ((double)((float)il2cpp_codegen_subtract(L_7, L_8)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:58>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = ___0_point;
		NullCheck(L_9);
		float L_10 = L_9->___z;
		float L_11 = __this->___z;
		V_3 = ((double)((float)il2cpp_codegen_subtract(L_10, L_11)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:59>
		float L_12 = __this->___x;
		V_4 = ((double)L_12);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:60>
		float L_13 = __this->___y;
		V_5 = ((double)L_13);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:61>
		float L_14 = __this->___z;
		V_6 = ((double)L_14);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:62>
		int32_t L_15 = ___1_count;
		V_7 = ((double)L_15);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:63>
		V_8 = 1;
		goto IL_00a4;
	}

IL_0061:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:64>
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:65>
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:66>
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:67>
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:68>
		double L_16 = V_4;
		double L_17 = V_1;
		int32_t L_18 = V_8;
		double L_19 = V_7;
		double L_20 = V_5;
		double L_21 = V_2;
		int32_t L_22 = V_8;
		double L_23 = V_7;
		double L_24 = V_6;
		double L_25 = V_3;
		int32_t L_26 = V_8;
		double L_27 = V_7;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_28 = (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)il2cpp_codegen_object_new(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F(L_28, ((float)((double)il2cpp_codegen_add(L_16, ((double)(((double)il2cpp_codegen_multiply(L_17, ((double)L_18)))/L_19))))), ((float)((double)il2cpp_codegen_add(L_20, ((double)(((double)il2cpp_codegen_multiply(L_21, ((double)L_22)))/L_23))))), ((float)((double)il2cpp_codegen_add(L_24, ((double)(((double)il2cpp_codegen_multiply(L_25, ((double)L_26)))/L_27))))), NULL);
		V_9 = L_28;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:69>
		GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* L_29 = ___2_checkPoint;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_30 = V_9;
		NullCheck(L_29);
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_31;
		L_31 = GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_inline(L_29, L_30, NULL);
		V_9 = L_31;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:70>
		List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* L_32 = V_0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_33 = V_9;
		NullCheck(L_32);
		List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_inline(L_32, L_33, List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:63>
		int32_t L_34 = V_8;
		V_8 = ((int32_t)il2cpp_codegen_add(L_34, 1));
	}

IL_00a4:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:63>
		int32_t L_35 = V_8;
		int32_t L_36 = ___1_count;
		if ((((int32_t)L_35) < ((int32_t)L_36)))
		{
			goto IL_0061;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:73>
		List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* L_37 = V_0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_38 = ___0_point;
		NullCheck(L_37);
		List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_inline(L_37, L_38, List_1_Add_m364E96E03D4030C4B72182E1877AFEF19D07A4F7_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:75>
		List_1_t235F0E4D512223F85AC89B0702A4E29311F57A1F* L_39 = V_0;
		return L_39;
	}
}
// Method Definition Index: 115362
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_ComputeProjectedVertex_m48B50AB8903161CF2286AB0D4DFBD74987AE50D3 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Math_tEB65DE7CA8B083C412C969C92981C030865486CE_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	double V_0 = 0.0;
	double V_1 = 0.0;
	double V_2 = 0.0;
	double V_3 = 0.0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:80>
		float L_0 = __this->___x;
		float L_1 = __this->___x;
		float L_2 = __this->___y;
		float L_3 = __this->___y;
		float L_4 = __this->___z;
		float L_5 = __this->___z;
		il2cpp_codegen_runtime_class_init_inline(Math_tEB65DE7CA8B083C412C969C92981C030865486CE_il2cpp_TypeInfo_var);
		double L_6;
		L_6 = sqrt(((double)il2cpp_codegen_add(((double)il2cpp_codegen_add(((double)il2cpp_codegen_multiply(((double)L_0), ((double)L_1))), ((double)il2cpp_codegen_multiply(((double)L_2), ((double)L_3))))), ((double)il2cpp_codegen_multiply(((double)L_4), ((double)L_5))))));
		V_0 = ((double)il2cpp_codegen_multiply((2.0), L_6));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:81>
		double L_7 = V_0;
		float L_8 = __this->____elevation;
		V_0 = ((double)(L_7/((double)il2cpp_codegen_add((1.0), ((double)L_8)))));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:82>
		float L_9 = __this->___x;
		double L_10 = V_0;
		V_1 = ((double)(((double)L_9)/L_10));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:83>
		float L_11 = __this->___y;
		double L_12 = V_0;
		V_2 = ((double)(((double)L_11)/L_12));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:84>
		float L_13 = __this->___z;
		double L_14 = V_0;
		V_3 = ((double)(((double)L_13)/L_14));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:85>
		double L_15 = V_1;
		double L_16 = V_2;
		double L_17 = V_3;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_18;
		memset((&L_18), 0, sizeof(L_18));
		Vector3__ctor_m376936E6B999EF1ECBE57D990A386303E2283DE0_inline((&L_18), ((float)L_15), ((float)L_16), ((float)L_17), NULL);
		__this->____projectedVector3 = L_18;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:86>
		__this->____projectedVector3Computed = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:87>
		return;
	}
}
// Method Definition Index: 115363
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_RegisterTriangle_mF61506CB9B7560D76421D17A8BF1757FB75EDD4C (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* ___0_triangle, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	int32_t V_0 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:90>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_0 = __this->___triangles;
		if (L_0)
		{
			goto IL_0014;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:91>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_1 = (TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452*)(TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452*)SZArrayNew(TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452_il2cpp_TypeInfo_var, (uint32_t)6);
		__this->___triangles = L_1;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___triangles), (void*)L_1);
	}

IL_0014:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:92>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_2 = __this->___triangles;
		int32_t L_3 = __this->___triangleCount;
		V_0 = L_3;
		int32_t L_4 = V_0;
		__this->___triangleCount = ((int32_t)il2cpp_codegen_add(L_4, 1));
		int32_t L_5 = V_0;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_6 = ___0_triangle;
		NullCheck(L_2);
		ArrayElementTypeCheck (L_2, L_6);
		(L_2)->SetAt(static_cast<il2cpp_array_size_t>(L_5), (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F*)L_6);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:93>
		return;
	}
}
// Method Definition Index: 115364
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Point_GetOrderedTriangles_m7F19272FCADBE86F98D99E4A5AC259F94122CC1B (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* ___0_tempTriangles, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	int32_t V_0 = 0;
	int32_t V_1 = 0;
	int32_t V_2 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:98>
		int32_t L_0 = __this->___triangleCount;
		if (L_0)
		{
			goto IL_000a;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:99>
		return 0;
	}

IL_000a:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:101>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_1 = ___0_tempTriangles;
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_2 = __this->___triangles;
		NullCheck(L_2);
		int32_t L_3 = 0;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_4 = (L_2)->GetAt(static_cast<il2cpp_array_size_t>(L_3));
		NullCheck(L_1);
		ArrayElementTypeCheck (L_1, L_4);
		(L_1)->SetAt(static_cast<il2cpp_array_size_t>(0), (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F*)L_4);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:102>
		V_0 = 1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:103>
		int32_t L_5 = ((Point_t13126743CEDB2A83E25B6018553E5022E06D2790_StaticFields*)il2cpp_codegen_static_fields_for(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var))->___flag;
		((Point_t13126743CEDB2A83E25B6018553E5022E06D2790_StaticFields*)il2cpp_codegen_static_fields_for(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var))->___flag = ((int32_t)il2cpp_codegen_add(L_5, 1));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:104>
		V_1 = 0;
		goto IL_008a;
	}

IL_0027:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:105>
		V_2 = 1;
		goto IL_007d;
	}

IL_002b:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:106>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_6 = __this->___triangles;
		int32_t L_7 = V_2;
		NullCheck(L_6);
		int32_t L_8 = L_7;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_9 = (L_6)->GetAt(static_cast<il2cpp_array_size_t>(L_8));
		NullCheck(L_9);
		int32_t L_10 = L_9->___getOrderedFlag;
		int32_t L_11 = ((Point_t13126743CEDB2A83E25B6018553E5022E06D2790_StaticFields*)il2cpp_codegen_static_fields_for(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var))->___flag;
		if ((((int32_t)L_10) == ((int32_t)L_11)))
		{
			goto IL_0079;
		}
	}
	{
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_12 = ___0_tempTriangles;
		int32_t L_13 = V_1;
		NullCheck(L_12);
		int32_t L_14 = L_13;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_15 = (L_12)->GetAt(static_cast<il2cpp_array_size_t>(L_14));
		if (!L_15)
		{
			goto IL_0079;
		}
	}
	{
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_16 = __this->___triangles;
		int32_t L_17 = V_2;
		NullCheck(L_16);
		int32_t L_18 = L_17;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_19 = (L_16)->GetAt(static_cast<il2cpp_array_size_t>(L_18));
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_20 = ___0_tempTriangles;
		int32_t L_21 = V_1;
		NullCheck(L_20);
		int32_t L_22 = L_21;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_23 = (L_20)->GetAt(static_cast<il2cpp_array_size_t>(L_22));
		NullCheck(L_19);
		bool L_24;
		L_24 = Triangle_isAdjacentTo_m7CF316F8E00DE3432EAA5C9C71C70AC2694FB94B(L_19, L_23, NULL);
		if (!L_24)
		{
			goto IL_0079;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:107>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_25 = ___0_tempTriangles;
		int32_t L_26 = V_0;
		int32_t L_27 = L_26;
		V_0 = ((int32_t)il2cpp_codegen_add(L_27, 1));
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_28 = __this->___triangles;
		int32_t L_29 = V_2;
		NullCheck(L_28);
		int32_t L_30 = L_29;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_31 = (L_28)->GetAt(static_cast<il2cpp_array_size_t>(L_30));
		NullCheck(L_25);
		ArrayElementTypeCheck (L_25, L_31);
		(L_25)->SetAt(static_cast<il2cpp_array_size_t>(L_27), (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F*)L_31);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:108>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_32 = __this->___triangles;
		int32_t L_33 = V_2;
		NullCheck(L_32);
		int32_t L_34 = L_33;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_35 = (L_32)->GetAt(static_cast<il2cpp_array_size_t>(L_34));
		int32_t L_36 = ((Point_t13126743CEDB2A83E25B6018553E5022E06D2790_StaticFields*)il2cpp_codegen_static_fields_for(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var))->___flag;
		NullCheck(L_35);
		L_35->___getOrderedFlag = L_36;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:109>
		goto IL_0086;
	}

IL_0079:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:105>
		int32_t L_37 = V_2;
		V_2 = ((int32_t)il2cpp_codegen_add(L_37, 1));
	}

IL_007d:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:105>
		int32_t L_38 = V_2;
		int32_t L_39 = __this->___triangleCount;
		if ((((int32_t)L_38) < ((int32_t)L_39)))
		{
			goto IL_002b;
		}
	}

IL_0086:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:104>
		int32_t L_40 = V_1;
		V_1 = ((int32_t)il2cpp_codegen_add(L_40, 1));
	}

IL_008a:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:104>
		int32_t L_41 = V_1;
		int32_t L_42 = __this->___triangleCount;
		if ((((int32_t)L_41) < ((int32_t)((int32_t)il2cpp_codegen_subtract(L_42, 1)))))
		{
			goto IL_0027;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:114>
		int32_t L_43 = V_0;
		return L_43;
	}
}
// Method Definition Index: 115365
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR String_t* Point_ToString_mEA06326F004B60052AC4C20CF6293C95A1BE3363 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteralC18C9BB6DF0D5C60CE5A5D2D3D6111BEB6F8CCEB);
		s_Il2CppMethodInitialized = true;
	}
	float V_0 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:119>
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_0 = (StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248*)(StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248*)SZArrayNew(StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248_il2cpp_TypeInfo_var, (uint32_t)5);
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_1 = L_0;
		float L_2 = __this->___x;
		V_0 = ((float)(((float)il2cpp_codegen_cast_double_to_int<int32_t>(((float)il2cpp_codegen_multiply(L_2, (100.0f)))))/(100.0f)));
		String_t* L_3;
		L_3 = Single_ToString_mE282EDA9CA4F7DF88432D807732837A629D04972((&V_0), NULL);
		NullCheck(L_1);
		(L_1)->SetAt(static_cast<il2cpp_array_size_t>(0), (String_t*)L_3);
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_4 = L_1;
		NullCheck(L_4);
		(L_4)->SetAt(static_cast<il2cpp_array_size_t>(1), (String_t*)_stringLiteralC18C9BB6DF0D5C60CE5A5D2D3D6111BEB6F8CCEB);
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_5 = L_4;
		float L_6 = __this->___y;
		V_0 = ((float)(((float)il2cpp_codegen_cast_double_to_int<int32_t>(((float)il2cpp_codegen_multiply(L_6, (100.0f)))))/(100.0f)));
		String_t* L_7;
		L_7 = Single_ToString_mE282EDA9CA4F7DF88432D807732837A629D04972((&V_0), NULL);
		NullCheck(L_5);
		(L_5)->SetAt(static_cast<il2cpp_array_size_t>(2), (String_t*)L_7);
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_8 = L_5;
		NullCheck(L_8);
		(L_8)->SetAt(static_cast<il2cpp_array_size_t>(3), (String_t*)_stringLiteralC18C9BB6DF0D5C60CE5A5D2D3D6111BEB6F8CCEB);
		StringU5BU5D_t7674CD946EC0CE7B3AE0BE70E6EE85F2ECD9F248* L_9 = L_8;
		float L_10 = __this->___z;
		V_0 = ((float)(((float)il2cpp_codegen_cast_double_to_int<int32_t>(((float)il2cpp_codegen_multiply(L_10, (100.0f)))))/(100.0f)));
		String_t* L_11;
		L_11 = Single_ToString_mE282EDA9CA4F7DF88432D807732837A629D04972((&V_0), NULL);
		NullCheck(L_9);
		(L_9)->SetAt(static_cast<il2cpp_array_size_t>(4), (String_t*)L_11);
		String_t* L_12;
		L_12 = String_Concat_m647EBF831F54B6DF7D5AFA5FD012CF4EE7571B6A(L_9, NULL);
		return L_12;
	}
}
// Method Definition Index: 115366
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Point_Equals_mCF391F9146A1C5D20594BE757B89550AAC968FED (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, RuntimeObject* ___0_obj, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_0 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:124>
		RuntimeObject* L_0 = ___0_obj;
		if (!((Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)IsInstClass((RuntimeObject*)L_0, Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var)))
		{
			goto IL_003c;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:125>
		RuntimeObject* L_1 = ___0_obj;
		V_0 = ((Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)CastclassClass((RuntimeObject*)L_1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:126>
		float L_2 = __this->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_3 = V_0;
		NullCheck(L_3);
		float L_4 = L_3->___x;
		if ((!(((float)L_2) == ((float)L_4))))
		{
			goto IL_003a;
		}
	}
	{
		float L_5 = __this->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = V_0;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		if ((!(((float)L_5) == ((float)L_7))))
		{
			goto IL_003a;
		}
	}
	{
		float L_8 = __this->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = V_0;
		NullCheck(L_9);
		float L_10 = L_9->___z;
		return (bool)((((float)L_8) == ((float)L_10))? 1 : 0);
	}

IL_003a:
	{
		return (bool)0;
	}

IL_003c:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:128>
		return (bool)0;
	}
}
// Method Definition Index: 115367
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Point_Equals_m3481BC8786AD1AAC87F41E4C8258A363743115D9 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p2, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:132>
		float L_0 = __this->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = ___0_p2;
		NullCheck(L_1);
		float L_2 = L_1->___x;
		if ((!(((float)L_0) == ((float)L_2))))
		{
			goto IL_002b;
		}
	}
	{
		float L_3 = __this->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_p2;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		if ((!(((float)L_3) == ((float)L_5))))
		{
			goto IL_002b;
		}
	}
	{
		float L_6 = __this->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_7 = ___0_p2;
		NullCheck(L_7);
		float L_8 = L_7->___z;
		return (bool)((((float)L_6) == ((float)L_8))? 1 : 0);
	}

IL_002b:
	{
		return (bool)0;
	}
}
// Method Definition Index: 115368
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Point_Equals_m72C0BE199DEC6CBDB6C2E7F638DC656A68E2639F (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___1_p2, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:136>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___0_p1;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___1_p2;
		NullCheck(L_2);
		float L_3 = L_2->___x;
		if ((!(((float)L_1) == ((float)L_3))))
		{
			goto IL_002b;
		}
	}
	{
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_p1;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___1_p2;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		if ((!(((float)L_5) == ((float)L_7))))
		{
			goto IL_002b;
		}
	}
	{
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = ___0_p1;
		NullCheck(L_8);
		float L_9 = L_8->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_10 = ___1_p2;
		NullCheck(L_10);
		float L_11 = L_10->___z;
		return (bool)((((float)L_9) == ((float)L_11))? 1 : 0);
	}

IL_002b:
	{
		return (bool)0;
	}
}
// Method Definition Index: 115369
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Point_GetHashCode_m4B55AB94DAE8E132E742EC13349CD5112B6FA13C (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:140>
		int32_t L_0 = __this->___hashCode;
		if (L_0)
		{
			goto IL_0035;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:141>
		float* L_1 = (float*)(&__this->___x);
		int32_t L_2;
		L_2 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_1, NULL);
		float* L_3 = (float*)(&__this->___y);
		int32_t L_4;
		L_4 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_3, NULL);
		float* L_5 = (float*)(&__this->___z);
		int32_t L_6;
		L_6 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_5, NULL);
		__this->___hashCode = ((int32_t)(((int32_t)(L_2^((int32_t)(L_4<<2))))^((int32_t)(L_6>>2))));
	}

IL_0035:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:143>
		int32_t L_7 = __this->___hashCode;
		return L_7;
	}
}
// Method Definition Index: 115370
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Point_GetHashCode_m5959D9B5CEE075C33F9CB3E4A46DFB6780F810F8 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:147>
		int32_t L_0 = __this->___hashCode;
		if (L_0)
		{
			goto IL_0035;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:148>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = ___0_p;
		NullCheck(L_1);
		float* L_2 = (float*)(&L_1->___x);
		int32_t L_3;
		L_3 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_2, NULL);
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_p;
		NullCheck(L_4);
		float* L_5 = (float*)(&L_4->___y);
		int32_t L_6;
		L_6 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_5, NULL);
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_7 = ___0_p;
		NullCheck(L_7);
		float* L_8 = (float*)(&L_7->___z);
		int32_t L_9;
		L_9 = Single_GetHashCode_mC3F1E099D1CF165C2D71FBCC5EF6A6792F9021D2(L_8, NULL);
		__this->___hashCode = ((int32_t)(((int32_t)(L_3^((int32_t)(L_6<<2))))^((int32_t)(L_9>>2))));
	}

IL_0035:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:150>
		int32_t L_10 = __this->___hashCode;
		return L_10;
	}
}
// Method Definition Index: 115371
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:154>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___0_point;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___0_point;
		NullCheck(L_2);
		float L_3 = L_2->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_point;
		NullCheck(L_4);
		float L_5 = L_4->___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6;
		memset((&L_6), 0, sizeof(L_6));
		Vector3__ctor_m376936E6B999EF1ECBE57D990A386303E2283DE0_inline((&L_6), L_1, L_3, L_5, NULL);
		return L_6;
	}
}
// Method Definition Index: 115372
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* Point_op_Multiply_mBE3E73FD2AD9F5D834571891766556C73773E0D4 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, float ___1_v, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:158>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___0_point;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		float L_2 = ___1_v;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_3 = ___0_point;
		NullCheck(L_3);
		float L_4 = L_3->___y;
		float L_5 = ___1_v;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___0_point;
		NullCheck(L_6);
		float L_7 = L_6->___z;
		float L_8 = ___1_v;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)il2cpp_codegen_object_new(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F(L_9, ((float)il2cpp_codegen_multiply(L_1, L_2)), ((float)il2cpp_codegen_multiply(L_4, L_5)), ((float)il2cpp_codegen_multiply(L_7, L_8)), NULL);
		return L_9;
	}
}
// Method Definition Index: 115373
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float Point_SqrDistance_m772074B58546A59C69EB6B5E090FCEBA499DA173 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___1_p2, const RuntimeMethod* method) 
{
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:162>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___1_p2;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___0_p1;
		NullCheck(L_2);
		float L_3 = L_2->___x;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:163>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___1_p2;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___0_p1;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		V_0 = ((float)il2cpp_codegen_subtract(L_5, L_7));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:164>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = ___1_p2;
		NullCheck(L_8);
		float L_9 = L_8->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_10 = ___0_p1;
		NullCheck(L_10);
		float L_11 = L_10->___z;
		V_1 = ((float)il2cpp_codegen_subtract(L_9, L_11));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:165>
		float L_12 = ((float)il2cpp_codegen_subtract(L_1, L_3));
		float L_13 = V_0;
		float L_14 = V_0;
		float L_15 = V_1;
		float L_16 = V_1;
		return ((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(((float)il2cpp_codegen_multiply(L_12, L_12)), ((float)il2cpp_codegen_multiply(L_13, L_14)))), ((float)il2cpp_codegen_multiply(L_15, L_16))));
	}
}
// Method Definition Index: 115374
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* Point_Average_mFBBCC6A9A895AFF4710D7F1AE4AD27B1165C1212 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___1_p2, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:169>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___0_p1;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___1_p2;
		NullCheck(L_2);
		float L_3 = L_2->___x;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:170>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_p1;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___1_p2;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		V_0 = ((float)il2cpp_codegen_multiply(((float)il2cpp_codegen_add(L_5, L_7)), (0.5f)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:171>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = ___0_p1;
		NullCheck(L_8);
		float L_9 = L_8->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_10 = ___1_p2;
		NullCheck(L_10);
		float L_11 = L_10->___z;
		V_1 = ((float)il2cpp_codegen_multiply(((float)il2cpp_codegen_add(L_9, L_11)), (0.5f)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:172>
		float L_12 = V_0;
		float L_13 = V_1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_14 = (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)il2cpp_codegen_object_new(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F(L_14, ((float)il2cpp_codegen_multiply(((float)il2cpp_codegen_add(L_1, L_3)), (0.5f))), L_12, L_13, NULL);
		return L_14;
	}
}
// Method Definition Index: 115375
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR float Point_Distance_m8FD84C3202D3C6CE34227E80B398CBF6A8D2F3C3 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___1_p2, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Math_tEB65DE7CA8B083C412C969C92981C030865486CE_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:176>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_0 = ___1_p2;
		NullCheck(L_0);
		float L_1 = L_0->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___0_p1;
		NullCheck(L_2);
		float L_3 = L_2->___x;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:177>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___1_p2;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___0_p1;
		NullCheck(L_6);
		float L_7 = L_6->___y;
		V_0 = ((float)il2cpp_codegen_subtract(L_5, L_7));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:178>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = ___1_p2;
		NullCheck(L_8);
		float L_9 = L_8->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_10 = ___0_p1;
		NullCheck(L_10);
		float L_11 = L_10->___z;
		V_1 = ((float)il2cpp_codegen_subtract(L_9, L_11));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:179>
		float L_12 = ((float)il2cpp_codegen_subtract(L_1, L_3));
		float L_13 = V_0;
		float L_14 = V_0;
		float L_15 = V_1;
		float L_16 = V_1;
		il2cpp_codegen_runtime_class_init_inline(Math_tEB65DE7CA8B083C412C969C92981C030865486CE_il2cpp_TypeInfo_var);
		double L_17;
		L_17 = sqrt(((double)((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(((float)il2cpp_codegen_multiply(L_12, L_12)), ((float)il2cpp_codegen_multiply(L_13, L_14)))), ((float)il2cpp_codegen_multiply(L_15, L_16))))));
		return ((float)L_17);
	}
}
// Method Definition Index: 115376
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_ClampDistance_m989A3AE6ECB5CB604630F7FC98957FF72B8798CB (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_center, float ___1_factor, const RuntimeMethod* method) 
{
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	float V_2 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:183>
		float L_0 = __this->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = ___0_center;
		NullCheck(L_1);
		float L_2 = L_1->___x;
		V_0 = ((float)il2cpp_codegen_subtract(L_0, L_2));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:184>
		float L_3 = __this->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_center;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		V_1 = ((float)il2cpp_codegen_subtract(L_3, L_5));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:185>
		float L_6 = __this->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_7 = ___0_center;
		NullCheck(L_7);
		float L_8 = L_7->___z;
		V_2 = ((float)il2cpp_codegen_subtract(L_6, L_8));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:186>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = ___0_center;
		NullCheck(L_9);
		float L_10 = L_9->___x;
		float L_11 = V_0;
		float L_12 = ___1_factor;
		__this->___x = ((float)il2cpp_codegen_add(L_10, ((float)il2cpp_codegen_multiply(L_11, L_12))));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:187>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_13 = ___0_center;
		NullCheck(L_13);
		float L_14 = L_13->___y;
		float L_15 = V_1;
		float L_16 = ___1_factor;
		__this->___y = ((float)il2cpp_codegen_add(L_14, ((float)il2cpp_codegen_multiply(L_15, L_16))));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:188>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_17 = ___0_center;
		NullCheck(L_17);
		float L_18 = L_17->___z;
		float L_19 = V_2;
		float L_20 = ___1_factor;
		__this->___z = ((float)il2cpp_codegen_add(L_18, ((float)il2cpp_codegen_multiply(L_19, L_20))));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:189>
		__this->____projectedVector3Computed = (bool)0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:190>
		return;
	}
}
// Method Definition Index: 115377
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_Add_mDB8112B63DE5327090757D979E1A78FDE907FB5E (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_p, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:193>
		float L_0 = __this->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = ___0_p;
		NullCheck(L_1);
		float L_2 = L_1->___x;
		__this->___x = ((float)il2cpp_codegen_add(L_0, L_2));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:194>
		float L_3 = __this->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___0_p;
		NullCheck(L_4);
		float L_5 = L_4->___y;
		__this->___y = ((float)il2cpp_codegen_add(L_3, L_5));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:195>
		float L_6 = __this->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_7 = ___0_p;
		NullCheck(L_7);
		float L_8 = L_7->___z;
		__this->___z = ((float)il2cpp_codegen_add(L_6, L_8));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:196>
		__this->____projectedVector3Computed = (bool)0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:197>
		return;
	}
}
// Method Definition Index: 115378
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Point_DivideBy_m18F72640F1E6431BD537F3240A555C3AA41924E5 (Point_t13126743CEDB2A83E25B6018553E5022E06D2790* __this, float ___0_d, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:200>
		float L_0 = __this->___x;
		float L_1 = ___0_d;
		__this->___x = ((float)(L_0/L_1));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:201>
		float L_2 = __this->___y;
		float L_3 = ___0_d;
		__this->___y = ((float)(L_2/L_3));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:202>
		float L_4 = __this->___z;
		float L_5 = ___0_d;
		__this->___z = ((float)(L_4/L_5));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:203>
		__this->____projectedVector3Computed = (bool)0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Point.cs:204>
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115379
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void ShaderParams__cctor_mC1C3AFB0F9D96F47745D759EB5A4DB78B223150F (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral04AD164E2A4DE9935B205DCA02B5501342A39890);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral47A3FAF17D89549FD0F0ECA7370B81F7C80DFCDE);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral4B8146FB95E4F51B29DA41EB5F6D60F8FD0ECF21);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral51282E2AAC09AC6EDBC2C1C237C0183F97FEE379);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral59861356BAB5171272E157858059C1801D7D5E5D);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral65DAFD279CC322209A6F3D846D770AA652BE1F34);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral67BEC592386C17C68CF044FFB14169A1073AC7EB);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral787984D270B549500FD6EE450785085D7058DF70);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:7>
		int32_t L_0;
		L_0 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral4B8146FB95E4F51B29DA41EB5F6D60F8FD0ECF21, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___MainTex = L_0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:8>
		int32_t L_1;
		L_1 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral67BEC592386C17C68CF044FFB14169A1073AC7EB, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___BaseMap = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:9>
		int32_t L_2;
		L_2 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral59861356BAB5171272E157858059C1801D7D5E5D, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___Color2 = L_2;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:10>
		int32_t L_3;
		L_3 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral47A3FAF17D89549FD0F0ECA7370B81F7C80DFCDE, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___Color = L_3;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:11>
		int32_t L_4;
		L_4 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral51282E2AAC09AC6EDBC2C1C237C0183F97FEE379, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___BaseColor = L_4;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:12>
		int32_t L_5;
		L_5 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral65DAFD279CC322209A6F3D846D770AA652BE1F34, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___TileAlpha = L_5;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:13>
		int32_t L_6;
		L_6 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral04AD164E2A4DE9935B205DCA02B5501342A39890, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___ColorShift = L_6;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/ShaderParams.cs:14>
		int32_t L_7;
		L_7 = Shader_PropertyToID_mE98523D50F5656CAE89B30695C458253EB8956CA(_stringLiteral787984D270B549500FD6EE450785085D7058DF70, NULL);
		((ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_StaticFields*)il2cpp_codegen_static_fields_for(ShaderParams_tF08A928BF8CC3DC5B7C436ADB3C2403A579B2101_il2cpp_TypeInfo_var))->___Center = L_7;
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115380
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TextureScaler_Scale_m9B39C7946A242A96D5BE1BD453A4914A791427EC (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___0_tex, int32_t ___1_width, int32_t ___2_height, int32_t ___3_mode, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:9>
		RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* L_0;
		L_0 = RenderTexture_get_active_mA4434B3E79DEF2C01CAE0A53061598B16443C9E7(NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:11>
		int32_t L_1 = ___1_width;
		int32_t L_2 = ___2_height;
		il2cpp_codegen_runtime_class_init_inline(Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D_il2cpp_TypeInfo_var);
		Rect__ctor_m18C3033D135097BEE424AAA68D91C706D2647F23_inline((&V_0), (0.0f), (0.0f), ((float)L_1), ((float)L_2), NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:12>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_3 = ___0_tex;
		int32_t L_4 = ___1_width;
		int32_t L_5 = ___2_height;
		int32_t L_6 = ___3_mode;
		TextureScaler__gpu_scale_m22CBF203D8BC668F378F1B797C02F61EB8624B60(L_3, L_4, L_5, L_6, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:15>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_7 = ___0_tex;
		int32_t L_8 = ___1_width;
		int32_t L_9 = ___2_height;
		NullCheck(L_7);
		bool L_10;
		L_10 = Texture2D_Reinitialize_m9AB4169DA359C18BB4102F8E00C4321B53714E6B(L_7, L_8, L_9, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:16>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_11 = ___0_tex;
		Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D L_12 = V_0;
		NullCheck(L_11);
		Texture2D_ReadPixels_m7483DB211233F02E46418E9A6077487925F0024C(L_11, L_12, 0, 0, (bool)1, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:17>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_13 = ___0_tex;
		NullCheck(L_13);
		Texture2D_Apply_mCC369BCAB2D3AD3EE50EE01DA67AF227865FA2B3(L_13, (bool)1, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:19>
		RenderTexture_set_active_m5EE8E2327EF9B306C1425014CC34C41A8384E7AB(L_0, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:20>
		return;
	}
}
// Method Definition Index: 115381
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TextureScaler__gpu_scale_m22CBF203D8BC668F378F1B797C02F61EB8624B60 (Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* ___0_src, int32_t ___1_width, int32_t ___2_height, int32_t ___3_fmode, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Graphics_t99CD970FFEA58171C70F54DF0C06D315BD452F2C_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:24>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_0 = ___0_src;
		int32_t L_1 = ___3_fmode;
		NullCheck(L_0);
		Texture_set_filterMode_mE423E58C0C16D059EA62BA87AD70F44AEA50CCC9(L_0, L_1, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:25>
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_2 = ___0_src;
		NullCheck(L_2);
		Texture2D_Apply_mCC369BCAB2D3AD3EE50EE01DA67AF227865FA2B3(L_2, (bool)1, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:28>
		int32_t L_3 = ___1_width;
		int32_t L_4 = ___2_height;
		RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27* L_5 = (RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27*)il2cpp_codegen_object_new(RenderTexture_tBA90C4C3AD9EECCFDDCC632D97C29FAB80D60D27_il2cpp_TypeInfo_var);
		RenderTexture__ctor_m45EACC89DDF408948889586516B3CA7AA8B73BFA(L_5, L_3, L_4, ((int32_t)32), NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:31>
		il2cpp_codegen_runtime_class_init_inline(Graphics_t99CD970FFEA58171C70F54DF0C06D315BD452F2C_il2cpp_TypeInfo_var);
		Graphics_SetRenderTarget_m995C0F14B97C5BF46CCF2E7EF410C1CC05C46409(L_5, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:34>
		GL_LoadPixelMatrix_mF1C5A4508C5F110512C116A5DDE7AB0483FE961A((0.0f), (1.0f), (1.0f), (0.0f), NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:37>
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_6;
		memset((&L_6), 0, sizeof(L_6));
		Color__ctor_m3786F0D6E510D9CFA544523A955870BD2A514C8C_inline((&L_6), (0.0f), (0.0f), (0.0f), (0.0f), NULL);
		GL_Clear_mA172E771FC32B516DB826F537832307C3A16BE09((bool)1, (bool)1, L_6, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:38>
		Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D L_7;
		memset((&L_7), 0, sizeof(L_7));
		Rect__ctor_m18C3033D135097BEE424AAA68D91C706D2647F23_inline((&L_7), (0.0f), (0.0f), (1.0f), (1.0f), NULL);
		Texture2D_tE6505BC111DD8A424A9DBE8E05D7D09E11FFFCF4* L_8 = ___0_src;
		Graphics_DrawTexture_m400F92CB13445A7BC054BC074B7073EA7E4B322F(L_7, L_8, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/TextureScaler.cs:40>
		return;
	}
}
// Method Definition Index: 115382
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void TextureScaler__ctor_m54129E256B3917C1D6B13177D9B6648A2018B2B2 (TextureScaler_t17AC3C253E6114048501AC8E81E36A0B0111AE2F* __this, const RuntimeMethod* method) 
{
	{
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115383
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Tile_get_isPentagon_m6671EE5DEDEC2C5765606BAA7BBD01D187F13523 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:24>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_0 = __this->___vertexPoints;
		NullCheck(L_0);
		return (bool)((((int32_t)((int32_t)(((RuntimeArray*)L_0)->max_length))) == ((int32_t)5))? 1 : 0);
	}
}
// Method Definition Index: 115384
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* Tile_get_vertices_m14C7232E1E42BDE6034221163C690300D3162B66 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:46>
		bool L_0 = __this->____verticesComputed;
		if (L_0)
		{
			goto IL_000e;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:47>
		Tile_ComputeVertices_mF2997F9195BAE507B4FC1541BE4C5A0042CDDA32(__this, NULL);
	}

IL_000e:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:49>
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_1 = __this->____vertices;
		return L_1;
	}
}
// Method Definition Index: 115385
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Tile_get_polygonCenter_m71FAADF3FE4C5A16A61FB764FC978777531EDF02 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_0;
	memset((&V_0), 0, sizeof(V_0));
	int32_t V_1 = 0;
	int32_t V_2 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:55>
		bool L_0 = __this->____verticesComputed;
		if (L_0)
		{
			goto IL_000e;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:56>
		Tile_ComputeVertices_mF2997F9195BAE507B4FC1541BE4C5A0042CDDA32(__this, NULL);
	}

IL_000e:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:58>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_1;
		L_1 = Vector3_get_zero_m0C1249C3F25B1C70EAD3CC8B31259975A457AE39_inline(NULL);
		V_0 = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:59>
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_2 = __this->____vertices;
		NullCheck(L_2);
		V_1 = ((int32_t)(((RuntimeArray*)L_2)->max_length));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:60>
		V_2 = 0;
		goto IL_0079;
	}

IL_0021:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:61>
		float* L_3 = (float*)(&(&V_0)->___x);
		float* L_4 = L_3;
		float L_5 = *((float*)L_4);
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_6 = __this->____vertices;
		int32_t L_7 = V_2;
		NullCheck(L_6);
		float L_8 = ((L_6)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_7)))->___x;
		*((float*)L_4) = (float)((float)il2cpp_codegen_add(L_5, L_8));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:62>
		float* L_9 = (float*)(&(&V_0)->___y);
		float* L_10 = L_9;
		float L_11 = *((float*)L_10);
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_12 = __this->____vertices;
		int32_t L_13 = V_2;
		NullCheck(L_12);
		float L_14 = ((L_12)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_13)))->___y;
		*((float*)L_10) = (float)((float)il2cpp_codegen_add(L_11, L_14));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:63>
		float* L_15 = (float*)(&(&V_0)->___z);
		float* L_16 = L_15;
		float L_17 = *((float*)L_16);
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_18 = __this->____vertices;
		int32_t L_19 = V_2;
		NullCheck(L_18);
		float L_20 = ((L_18)->GetAddressAt(static_cast<il2cpp_array_size_t>(L_19)))->___z;
		*((float*)L_16) = (float)((float)il2cpp_codegen_add(L_17, L_20));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:60>
		int32_t L_21 = V_2;
		V_2 = ((int32_t)il2cpp_codegen_add(L_21, 1));
	}

IL_0079:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:60>
		int32_t L_22 = V_2;
		int32_t L_23 = V_1;
		if ((((int32_t)L_22) < ((int32_t)L_23)))
		{
			goto IL_0021;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:65>
		float* L_24 = (float*)(&(&V_0)->___x);
		float* L_25 = L_24;
		float L_26 = *((float*)L_25);
		int32_t L_27 = V_1;
		*((float*)L_25) = (float)((float)(L_26/((float)L_27)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:66>
		float* L_28 = (float*)(&(&V_0)->___y);
		float* L_29 = L_28;
		float L_30 = *((float*)L_29);
		int32_t L_31 = V_1;
		*((float*)L_29) = (float)((float)(L_30/((float)L_31)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:67>
		float* L_32 = (float*)(&(&V_0)->___z);
		float* L_33 = L_32;
		float L_34 = *((float*)L_33);
		int32_t L_35 = V_1;
		*((float*)L_33) = (float)((float)(L_34/((float)L_35)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:68>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_36 = V_0;
		return L_36;
	}
}
// Method Definition Index: 115386
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* Tile_get_neighbours_m14DFD2E6D44DDD879E8FAB9E6472026E3255D7EE (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:78>
		bool L_0 = __this->____neighboursComputed;
		if (L_0)
		{
			goto IL_000e;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:79>
		Tile_ComputeNeighbours_mD86CFDBC54BCB4BD622E5CAEE85E22DBDDEECEEC(__this, NULL);
	}

IL_000e:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:81>
		TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* L_1 = __this->____neighbours;
		return L_1;
	}
}
// Method Definition Index: 115387
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* Tile_get_neighboursIndices_m9617341F6C07CE1EEA8DEF1DE640E6E2C195A91A (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:91>
		bool L_0 = __this->____neighboursComputed;
		if (L_0)
		{
			goto IL_000e;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:92>
		Tile_ComputeNeighbours_mD86CFDBC54BCB4BD622E5CAEE85E22DBDDEECEEC(__this, NULL);
	}

IL_000e:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:94>
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_1 = __this->____neighboursIndices;
		return L_1;
	}
}
// Method Definition Index: 115388
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile__ctor_m6F1CE1952EE8A967AA9FE50E27CA3B333030FFE7 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_centerPoint, int32_t ___1_index, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	int32_t V_0 = 0;
	int32_t V_1 = 0;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_2;
	memset((&V_2), 0, sizeof(V_2));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_3;
	memset((&V_3), 0, sizeof(V_3));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_4;
	memset((&V_4), 0, sizeof(V_4));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_5;
	memset((&V_5), 0, sizeof(V_5));
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_6 = NULL;
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_7;
	memset((&V_7), 0, sizeof(V_7));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_8;
	memset((&V_8), 0, sizeof(V_8));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_9;
	memset((&V_9), 0, sizeof(V_9));
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_10;
	memset((&V_10), 0, sizeof(V_10));
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_11 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:106>
		__this->___canCross = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:111>
		__this->___group = 1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:166>
		__this->___crossCost = (1.0f);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:184>
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:185>
		int32_t L_0 = ___1_index;
		__this->___index = L_0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:186>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = ___0_centerPoint;
		__this->___centerPoint = L_1;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___centerPoint), (void*)L_1);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:187>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = __this->___centerPoint;
		NullCheck(L_2);
		L_2->___tile = __this;
		Il2CppCodeGenWriteBarrier((void**)(&L_2->___tile), (void*)__this);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:188>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_3 = ___0_centerPoint;
		NullCheck(L_3);
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4;
		L_4 = Point_get_projectedVector3_m173ED0275B0A7F93BCE5B23F34BFA602C68F33D6(L_3, NULL);
		__this->___center = L_4;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:189>
		__this->___visible = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:190>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_5 = ___0_centerPoint;
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_6 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempTriangles;
		NullCheck(L_5);
		int32_t L_7;
		L_7 = Point_GetOrderedTriangles_m7F19272FCADBE86F98D99E4A5AC259F94122CC1B(L_5, L_6, NULL);
		V_0 = L_7;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:191>
		int32_t L_8 = V_0;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_9 = (PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2*)(PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2*)SZArrayNew(PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2_il2cpp_TypeInfo_var, (uint32_t)L_8);
		__this->___vertexPoints = L_9;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___vertexPoints), (void*)L_9);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:193>
		V_1 = 0;
		goto IL_0080;
	}

IL_0068:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:194>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_10 = __this->___vertexPoints;
		int32_t L_11 = V_1;
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_12 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempTriangles;
		int32_t L_13 = V_1;
		NullCheck(L_12);
		int32_t L_14 = L_13;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_15 = (L_12)->GetAt(static_cast<il2cpp_array_size_t>(L_14));
		NullCheck(L_15);
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_16;
		L_16 = Triangle_GetCentroid_mA3FC4743A9681A58A97A61FEA04E4CE2D88C57DD(L_15, NULL);
		NullCheck(L_10);
		ArrayElementTypeCheck (L_10, L_16);
		(L_10)->SetAt(static_cast<il2cpp_array_size_t>(L_11), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_16);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:193>
		int32_t L_17 = V_1;
		V_1 = ((int32_t)il2cpp_codegen_add(L_17, 1));
	}

IL_0080:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:193>
		int32_t L_18 = V_1;
		int32_t L_19 = V_0;
		if ((((int32_t)L_18) < ((int32_t)L_19)))
		{
			goto IL_0068;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:198>
		int32_t L_20 = V_0;
		if ((!(((uint32_t)L_20) == ((uint32_t)6))))
		{
			goto IL_014b;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:199>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_21 = __this->___vertexPoints;
		NullCheck(L_21);
		int32_t L_22 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_23 = (L_21)->GetAt(static_cast<il2cpp_array_size_t>(L_22));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_24;
		L_24 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_23, NULL);
		V_2 = L_24;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:200>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_25 = __this->___vertexPoints;
		NullCheck(L_25);
		int32_t L_26 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_27 = (L_25)->GetAt(static_cast<il2cpp_array_size_t>(L_26));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_28;
		L_28 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_27, NULL);
		V_3 = L_28;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:201>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_29 = __this->___vertexPoints;
		NullCheck(L_29);
		int32_t L_30 = 5;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_31 = (L_29)->GetAt(static_cast<il2cpp_array_size_t>(L_30));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_32;
		L_32 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_31, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:202>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_33 = V_3;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_34 = V_2;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_35;
		L_35 = Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline(L_33, L_34, NULL);
		V_4 = L_35;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:203>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_36 = V_2;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_37;
		L_37 = Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline(L_32, L_36, NULL);
		V_5 = L_37;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:204>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_38 = V_4;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_39 = V_5;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_40;
		L_40 = Vector3_Cross_mF93A280558BCE756D13B6CC5DCD7DE8A43148987_inline(L_38, L_39, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:205>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_41 = V_3;
		float L_42;
		L_42 = Vector3_Dot_mBB86BB940AA0A32FA7D3C02AC42E5BC7095A5D52_inline(L_40, L_41, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:206>
		if ((!(((float)L_42) < ((float)(0.0f)))))
		{
			goto IL_01f0;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:208>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_43 = __this->___vertexPoints;
		NullCheck(L_43);
		int32_t L_44 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_45 = (L_43)->GetAt(static_cast<il2cpp_array_size_t>(L_44));
		V_6 = L_45;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:209>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_46 = __this->___vertexPoints;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_47 = __this->___vertexPoints;
		NullCheck(L_47);
		int32_t L_48 = 5;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_49 = (L_47)->GetAt(static_cast<il2cpp_array_size_t>(L_48));
		NullCheck(L_46);
		ArrayElementTypeCheck (L_46, L_49);
		(L_46)->SetAt(static_cast<il2cpp_array_size_t>(0), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_49);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:210>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_50 = __this->___vertexPoints;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_51 = V_6;
		NullCheck(L_50);
		ArrayElementTypeCheck (L_50, L_51);
		(L_50)->SetAt(static_cast<il2cpp_array_size_t>(5), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_51);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:211>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_52 = __this->___vertexPoints;
		NullCheck(L_52);
		int32_t L_53 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_54 = (L_52)->GetAt(static_cast<il2cpp_array_size_t>(L_53));
		V_6 = L_54;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:212>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_55 = __this->___vertexPoints;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_56 = __this->___vertexPoints;
		NullCheck(L_56);
		int32_t L_57 = 4;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_58 = (L_56)->GetAt(static_cast<il2cpp_array_size_t>(L_57));
		NullCheck(L_55);
		ArrayElementTypeCheck (L_55, L_58);
		(L_55)->SetAt(static_cast<il2cpp_array_size_t>(1), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_58);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:213>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_59 = __this->___vertexPoints;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_60 = V_6;
		NullCheck(L_59);
		ArrayElementTypeCheck (L_59, L_60);
		(L_59)->SetAt(static_cast<il2cpp_array_size_t>(4), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_60);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:214>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_61 = __this->___vertexPoints;
		NullCheck(L_61);
		int32_t L_62 = 2;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_63 = (L_61)->GetAt(static_cast<il2cpp_array_size_t>(L_62));
		V_6 = L_63;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:215>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_64 = __this->___vertexPoints;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_65 = __this->___vertexPoints;
		NullCheck(L_65);
		int32_t L_66 = 3;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_67 = (L_65)->GetAt(static_cast<il2cpp_array_size_t>(L_66));
		NullCheck(L_64);
		ArrayElementTypeCheck (L_64, L_67);
		(L_64)->SetAt(static_cast<il2cpp_array_size_t>(2), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_67);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:216>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_68 = __this->___vertexPoints;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_69 = V_6;
		NullCheck(L_68);
		ArrayElementTypeCheck (L_68, L_69);
		(L_68)->SetAt(static_cast<il2cpp_array_size_t>(3), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_69);
		return;
	}

IL_014b:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:218>
		int32_t L_70 = V_0;
		if ((!(((uint32_t)L_70) == ((uint32_t)5))))
		{
			goto IL_01f0;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:219>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_71 = __this->___vertexPoints;
		NullCheck(L_71);
		int32_t L_72 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_73 = (L_71)->GetAt(static_cast<il2cpp_array_size_t>(L_72));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_74;
		L_74 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_73, NULL);
		V_7 = L_74;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:220>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_75 = __this->___vertexPoints;
		NullCheck(L_75);
		int32_t L_76 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_77 = (L_75)->GetAt(static_cast<il2cpp_array_size_t>(L_76));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_78;
		L_78 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_77, NULL);
		V_8 = L_78;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:221>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_79 = __this->___vertexPoints;
		NullCheck(L_79);
		int32_t L_80 = 4;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_81 = (L_79)->GetAt(static_cast<il2cpp_array_size_t>(L_80));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_82;
		L_82 = Point_op_Explicit_mD21E7405C7D96E09B5B91BD1C8E010F7AFDCE084(L_81, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:222>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_83 = V_8;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_84 = V_7;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_85;
		L_85 = Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline(L_83, L_84, NULL);
		V_9 = L_85;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:223>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_86 = V_7;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_87;
		L_87 = Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline(L_82, L_86, NULL);
		V_10 = L_87;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:224>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_88 = V_9;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_89 = V_10;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_90;
		L_90 = Vector3_Cross_mF93A280558BCE756D13B6CC5DCD7DE8A43148987_inline(L_88, L_89, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:225>
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_91 = V_8;
		float L_92;
		L_92 = Vector3_Dot_mBB86BB940AA0A32FA7D3C02AC42E5BC7095A5D52_inline(L_90, L_91, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:226>
		if ((!(((float)L_92) < ((float)(0.0f)))))
		{
			goto IL_01f0;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:228>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_93 = __this->___vertexPoints;
		NullCheck(L_93);
		int32_t L_94 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_95 = (L_93)->GetAt(static_cast<il2cpp_array_size_t>(L_94));
		V_11 = L_95;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:229>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_96 = __this->___vertexPoints;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_97 = __this->___vertexPoints;
		NullCheck(L_97);
		int32_t L_98 = 4;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_99 = (L_97)->GetAt(static_cast<il2cpp_array_size_t>(L_98));
		NullCheck(L_96);
		ArrayElementTypeCheck (L_96, L_99);
		(L_96)->SetAt(static_cast<il2cpp_array_size_t>(0), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_99);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:230>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_100 = __this->___vertexPoints;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_101 = V_11;
		NullCheck(L_100);
		ArrayElementTypeCheck (L_100, L_101);
		(L_100)->SetAt(static_cast<il2cpp_array_size_t>(4), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_101);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:231>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_102 = __this->___vertexPoints;
		NullCheck(L_102);
		int32_t L_103 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_104 = (L_102)->GetAt(static_cast<il2cpp_array_size_t>(L_103));
		V_11 = L_104;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:232>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_105 = __this->___vertexPoints;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_106 = __this->___vertexPoints;
		NullCheck(L_106);
		int32_t L_107 = 3;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_108 = (L_106)->GetAt(static_cast<il2cpp_array_size_t>(L_107));
		NullCheck(L_105);
		ArrayElementTypeCheck (L_105, L_108);
		(L_105)->SetAt(static_cast<il2cpp_array_size_t>(1), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_108);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:233>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_109 = __this->___vertexPoints;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_110 = V_11;
		NullCheck(L_109);
		ArrayElementTypeCheck (L_109, L_110);
		(L_109)->SetAt(static_cast<il2cpp_array_size_t>(3), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_110);
	}

IL_01f0:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:236>
		return;
	}
}
// Method Definition Index: 115389
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile_ComputeNeighbours_mD86CFDBC54BCB4BD622E5CAEE85E22DBDDEECEEC (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Add_m7B178FDE6A5885D6C5CA3B7B4526898D85E95FA2_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Clear_mEA2D1EBD5CD934C78BB6B4022108C7CF1EB32C98_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_ToArray_mA192205F4E984425407DF97AF1E772728F7BDB51_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	int32_t V_0 = 0;
	Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* V_1 = NULL;
	int32_t V_2 = 0;
	Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* V_3 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:242>
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* L_0 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt;
		NullCheck(L_0);
		List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_inline(L_0, List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:243>
		List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* L_1 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___temp;
		NullCheck(L_1);
		List_1_Clear_mEA2D1EBD5CD934C78BB6B4022108C7CF1EB32C98_inline(L_1, List_1_Clear_mEA2D1EBD5CD934C78BB6B4022108C7CF1EB32C98_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:244>
		V_0 = 0;
		goto IL_0084;
	}

IL_0018:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:245>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = __this->___centerPoint;
		NullCheck(L_2);
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_3 = L_2->___triangles;
		int32_t L_4 = V_0;
		NullCheck(L_3);
		int32_t L_5 = L_4;
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_6 = (L_3)->GetAt(static_cast<il2cpp_array_size_t>(L_5));
		V_1 = L_6;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:246>
		V_2 = 0;
		goto IL_007c;
	}

IL_002a:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:247>
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_7 = V_1;
		NullCheck(L_7);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_8 = L_7->___points;
		int32_t L_9 = V_2;
		NullCheck(L_8);
		int32_t L_10 = L_9;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_11 = (L_8)->GetAt(static_cast<il2cpp_array_size_t>(L_10));
		NullCheck(L_11);
		Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* L_12 = L_11->___tile;
		V_3 = L_12;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:248>
		Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* L_13 = V_3;
		if (!L_13)
		{
			goto IL_0078;
		}
	}
	{
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_14 = V_1;
		NullCheck(L_14);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_15 = L_14->___points;
		int32_t L_16 = V_2;
		NullCheck(L_15);
		int32_t L_17 = L_16;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_18 = (L_15)->GetAt(static_cast<il2cpp_array_size_t>(L_17));
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_19 = __this->___centerPoint;
		if ((((RuntimeObject*)(Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_18) == ((RuntimeObject*)(Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_19)))
		{
			goto IL_0078;
		}
	}
	{
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* L_20 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt;
		Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* L_21 = V_3;
		NullCheck(L_21);
		int32_t L_22 = L_21->___index;
		NullCheck(L_20);
		bool L_23;
		L_23 = List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B(L_20, L_22, List_1_Contains_m4FD96E89F15844C90032C7386BAB528817F1FF5B_RuntimeMethod_var);
		if (L_23)
		{
			goto IL_0078;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:249>
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* L_24 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___temp;
		Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* L_25 = V_3;
		NullCheck(L_24);
		List_1_Add_m7B178FDE6A5885D6C5CA3B7B4526898D85E95FA2_inline(L_24, L_25, List_1_Add_m7B178FDE6A5885D6C5CA3B7B4526898D85E95FA2_RuntimeMethod_var);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:250>
		List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* L_26 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt;
		Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* L_27 = V_3;
		NullCheck(L_27);
		int32_t L_28 = L_27->___index;
		NullCheck(L_26);
		List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_inline(L_26, L_28, List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_RuntimeMethod_var);
	}

IL_0078:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:246>
		int32_t L_29 = V_2;
		V_2 = ((int32_t)il2cpp_codegen_add(L_29, 1));
	}

IL_007c:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:246>
		int32_t L_30 = V_2;
		if ((((int32_t)L_30) < ((int32_t)3)))
		{
			goto IL_002a;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:244>
		int32_t L_31 = V_0;
		V_0 = ((int32_t)il2cpp_codegen_add(L_31, 1));
	}

IL_0084:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:244>
		int32_t L_32 = V_0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_33 = __this->___centerPoint;
		NullCheck(L_33);
		int32_t L_34 = L_33->___triangleCount;
		if ((((int32_t)L_32) < ((int32_t)L_34)))
		{
			goto IL_0018;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:254>
		il2cpp_codegen_runtime_class_init_inline(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* L_35 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___temp;
		NullCheck(L_35);
		TileU5BU5D_t80464C127442B698EA2C216209F42194F7DA7806* L_36;
		L_36 = List_1_ToArray_mA192205F4E984425407DF97AF1E772728F7BDB51(L_35, List_1_ToArray_mA192205F4E984425407DF97AF1E772728F7BDB51_RuntimeMethod_var);
		__this->____neighbours = L_36;
		Il2CppCodeGenWriteBarrier((void**)(&__this->____neighbours), (void*)L_36);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:255>
		List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* L_37 = ((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt;
		NullCheck(L_37);
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_38;
		L_38 = List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA(L_37, List_1_ToArray_m65479FB75A5FE539EA1A0D6681172717D23CEAAA_RuntimeMethod_var);
		__this->____neighboursIndices = L_38;
		Il2CppCodeGenWriteBarrier((void**)(&__this->____neighboursIndices), (void*)L_38);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:256>
		__this->____neighboursComputed = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:257>
		return;
	}
}
// Method Definition Index: 115390
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile_ComputeVertices_mF2997F9195BAE507B4FC1541BE4C5A0042CDDA32 (Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	int32_t V_0 = 0;
	int32_t V_1 = 0;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:260>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_0 = __this->___vertexPoints;
		NullCheck(L_0);
		V_0 = ((int32_t)(((RuntimeArray*)L_0)->max_length));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:261>
		int32_t L_1 = V_0;
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_2 = (Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C*)(Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C*)SZArrayNew(Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C_il2cpp_TypeInfo_var, (uint32_t)L_1);
		__this->____vertices = L_2;
		Il2CppCodeGenWriteBarrier((void**)(&__this->____vertices), (void*)L_2);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:262>
		V_1 = 0;
		goto IL_0036;
	}

IL_0019:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:263>
		Vector3U5BU5D_tFF1859CCE176131B909E2044F76443064254679C* L_3 = __this->____vertices;
		int32_t L_4 = V_1;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_5 = __this->___vertexPoints;
		int32_t L_6 = V_1;
		NullCheck(L_5);
		int32_t L_7 = L_6;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = (L_5)->GetAt(static_cast<il2cpp_array_size_t>(L_7));
		NullCheck(L_8);
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_9;
		L_9 = Point_get_projectedVector3_m173ED0275B0A7F93BCE5B23F34BFA602C68F33D6(L_8, NULL);
		NullCheck(L_3);
		(L_3)->SetAt(static_cast<il2cpp_array_size_t>(L_4), (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2)L_9);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:262>
		int32_t L_10 = V_1;
		V_1 = ((int32_t)il2cpp_codegen_add(L_10, 1));
	}

IL_0036:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:262>
		int32_t L_11 = V_1;
		int32_t L_12 = V_0;
		if ((((int32_t)L_11) < ((int32_t)L_12)))
		{
			goto IL_0019;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:265>
		__this->____verticesComputed = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:266>
		return;
	}
}
// Method Definition Index: 115391
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Tile__cctor_mBB304BC8CADC2BE70CB506C14E55D4B53559C3DD (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1__ctor_m47D3709632F94FA2260DFD6A32BF6B3A095A451D_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:182>
		TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452* L_0 = (TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452*)(TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452*)SZArrayNew(TriangleU5BU5D_t22D3F3FF7698A9A180F0F56CE30E11BA895E1452_il2cpp_TypeInfo_var, (uint32_t)((int32_t)20));
		((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempTriangles = L_0;
		Il2CppCodeGenWriteBarrier((void**)(&((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempTriangles), (void*)L_0);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:238>
		List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* L_1 = (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73*)il2cpp_codegen_object_new(List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73_il2cpp_TypeInfo_var);
		List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98(L_1, 6, List_1__ctor_m30DD6F0F8DFBA9856BF7220A3CDB1C89ECEC0D98_RuntimeMethod_var);
		((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt = L_1;
		Il2CppCodeGenWriteBarrier((void**)(&((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___tempInt), (void*)L_1);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Tile.cs:239>
		List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5* L_2 = (List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5*)il2cpp_codegen_object_new(List_1_t1CD56E32C92480BACEBFEDDA9B5ADBB3630162C5_il2cpp_TypeInfo_var);
		List_1__ctor_m47D3709632F94FA2260DFD6A32BF6B3A095A451D(L_2, 6, List_1__ctor_m47D3709632F94FA2260DFD6A32BF6B3A095A451D_RuntimeMethod_var);
		((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___temp = L_2;
		Il2CppCodeGenWriteBarrier((void**)(&((Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_StaticFields*)il2cpp_codegen_static_fields_for(Tile_t0AB54A26F90D3980CB21B959D84EB5D0C84ABC67_il2cpp_TypeInfo_var))->___temp), (void*)L_2);
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_pinvoke(const TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F& unmarshaled, TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_pinvoke& marshaled)
{
	marshaled.___tileIndex = unmarshaled.___tileIndex;
	marshaled.___color = unmarshaled.___color;
	marshaled.___textureIndex = unmarshaled.___textureIndex;
	marshaled.___tag = il2cpp_codegen_marshal_string(unmarshaled.___tag);
	marshaled.___tagInt = unmarshaled.___tagInt;
}
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_pinvoke_back(const TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_pinvoke& marshaled, TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F& unmarshaled)
{
	int32_t unmarshaledtileIndex_temp_0 = 0;
	unmarshaledtileIndex_temp_0 = marshaled.___tileIndex;
	unmarshaled.___tileIndex = unmarshaledtileIndex_temp_0;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F unmarshaledcolor_temp_1;
	memset((&unmarshaledcolor_temp_1), 0, sizeof(unmarshaledcolor_temp_1));
	unmarshaledcolor_temp_1 = marshaled.___color;
	unmarshaled.___color = unmarshaledcolor_temp_1;
	int32_t unmarshaledtextureIndex_temp_2 = 0;
	unmarshaledtextureIndex_temp_2 = marshaled.___textureIndex;
	unmarshaled.___textureIndex = unmarshaledtextureIndex_temp_2;
	unmarshaled.___tag = il2cpp_codegen_marshal_string_result(marshaled.___tag);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___tag), (void*)il2cpp_codegen_marshal_string_result(marshaled.___tag));
	int32_t unmarshaledtagInt_temp_4 = 0;
	unmarshaledtagInt_temp_4 = marshaled.___tagInt;
	unmarshaled.___tagInt = unmarshaledtagInt_temp_4;
}
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_pinvoke_cleanup(TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_pinvoke& marshaled)
{
	il2cpp_codegen_marshal_free(marshaled.___tag);
	marshaled.___tag = NULL;
}
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_com(const TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F& unmarshaled, TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_com& marshaled)
{
	marshaled.___tileIndex = unmarshaled.___tileIndex;
	marshaled.___color = unmarshaled.___color;
	marshaled.___textureIndex = unmarshaled.___textureIndex;
	marshaled.___tag = il2cpp_codegen_marshal_bstring(unmarshaled.___tag);
	marshaled.___tagInt = unmarshaled.___tagInt;
}
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_com_back(const TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_com& marshaled, TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F& unmarshaled)
{
	int32_t unmarshaledtileIndex_temp_0 = 0;
	unmarshaledtileIndex_temp_0 = marshaled.___tileIndex;
	unmarshaled.___tileIndex = unmarshaledtileIndex_temp_0;
	Color_tD001788D726C3A7F1379BEED0260B9591F440C1F unmarshaledcolor_temp_1;
	memset((&unmarshaledcolor_temp_1), 0, sizeof(unmarshaledcolor_temp_1));
	unmarshaledcolor_temp_1 = marshaled.___color;
	unmarshaled.___color = unmarshaledcolor_temp_1;
	int32_t unmarshaledtextureIndex_temp_2 = 0;
	unmarshaledtextureIndex_temp_2 = marshaled.___textureIndex;
	unmarshaled.___textureIndex = unmarshaledtextureIndex_temp_2;
	unmarshaled.___tag = il2cpp_codegen_marshal_bstring_result(marshaled.___tag);
	Il2CppCodeGenWriteBarrier((void**)(&unmarshaled.___tag), (void*)il2cpp_codegen_marshal_bstring_result(marshaled.___tag));
	int32_t unmarshaledtagInt_temp_4 = 0;
	unmarshaledtagInt_temp_4 = marshaled.___tagInt;
	unmarshaled.___tagInt = unmarshaledtagInt_temp_4;
}
IL2CPP_EXTERN_C void TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshal_com_cleanup(TileSaveData_t9F651F16D90A98E2BAF48FBD8BE6E73C4E36097F_marshaled_com& marshaled)
{
	il2cpp_codegen_marshal_free_bstring(marshaled.___tag);
	marshaled.___tag = NULL;
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115392
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereSaveData__ctor_m4969E618A30E9F6F49629105680319EDCA8D4CB1 (HexasphereSaveData_tE176CBF1E9D43C2C71D732FB61214E29E3909846* __this, const RuntimeMethod* method) 
{
	{
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115393
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void Triangle__ctor_m0D58A9CDA9890E2401A3BD2E51DF9143B11783B3 (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point1, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___1_point2, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___2_point3, bool ___3_register, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:16>
		Object__ctor_mE837C6B9FA8C6D5D109F4B2EC885D79919AC0EA2(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:17>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_0 = (PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2*)(PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2*)SZArrayNew(PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2_il2cpp_TypeInfo_var, (uint32_t)3);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_1 = L_0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_2 = ___0_point1;
		NullCheck(L_1);
		ArrayElementTypeCheck (L_1, L_2);
		(L_1)->SetAt(static_cast<il2cpp_array_size_t>(0), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_2);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_3 = L_1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = ___1_point2;
		NullCheck(L_3);
		ArrayElementTypeCheck (L_3, L_4);
		(L_3)->SetAt(static_cast<il2cpp_array_size_t>(1), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_4);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_5 = L_3;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_6 = ___2_point3;
		NullCheck(L_5);
		ArrayElementTypeCheck (L_5, L_6);
		(L_5)->SetAt(static_cast<il2cpp_array_size_t>(2), (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)L_6);
		__this->___points = L_5;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___points), (void*)L_5);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:18>
		bool L_7 = ___3_register;
		if (!L_7)
		{
			goto IL_0037;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:19>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = ___0_point1;
		NullCheck(L_8);
		Point_RegisterTriangle_mF61506CB9B7560D76421D17A8BF1757FB75EDD4C(L_8, __this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:20>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = ___1_point2;
		NullCheck(L_9);
		Point_RegisterTriangle_mF61506CB9B7560D76421D17A8BF1757FB75EDD4C(L_9, __this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:21>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_10 = ___2_point3;
		NullCheck(L_10);
		Point_RegisterTriangle_mF61506CB9B7560D76421D17A8BF1757FB75EDD4C(L_10, __this, NULL);
	}

IL_0037:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:23>
		return;
	}
}
// Method Definition Index: 115394
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool Triangle_isAdjacentTo_m7CF316F8E00DE3432EAA5C9C71C70AC2694FB94B (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* __this, Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* ___0_tri2, const RuntimeMethod* method) 
{
	bool V_0 = false;
	int32_t V_1 = 0;
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_2 = NULL;
	int32_t V_3 = 0;
	Point_t13126743CEDB2A83E25B6018553E5022E06D2790* V_4 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:27>
		V_0 = (bool)0;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:28>
		V_1 = 0;
		goto IL_005d;
	}

IL_0006:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:29>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_0 = __this->___points;
		int32_t L_1 = V_1;
		NullCheck(L_0);
		int32_t L_2 = L_1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_3 = (L_0)->GetAt(static_cast<il2cpp_array_size_t>(L_2));
		V_2 = L_3;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:30>
		V_3 = 0;
		goto IL_0055;
	}

IL_0013:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:31>
		Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* L_4 = ___0_tri2;
		NullCheck(L_4);
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_5 = L_4->___points;
		int32_t L_6 = V_3;
		NullCheck(L_5);
		int32_t L_7 = L_6;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = (L_5)->GetAt(static_cast<il2cpp_array_size_t>(L_7));
		V_4 = L_8;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:32>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_9 = V_2;
		NullCheck(L_9);
		float L_10 = L_9->___x;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_11 = V_4;
		NullCheck(L_11);
		float L_12 = L_11->___x;
		if ((!(((float)L_10) == ((float)L_12))))
		{
			goto IL_0051;
		}
	}
	{
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_13 = V_2;
		NullCheck(L_13);
		float L_14 = L_13->___y;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_15 = V_4;
		NullCheck(L_15);
		float L_16 = L_15->___y;
		if ((!(((float)L_14) == ((float)L_16))))
		{
			goto IL_0051;
		}
	}
	{
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_17 = V_2;
		NullCheck(L_17);
		float L_18 = L_17->___z;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_19 = V_4;
		NullCheck(L_19);
		float L_20 = L_19->___z;
		if ((!(((float)L_18) == ((float)L_20))))
		{
			goto IL_0051;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:33>
		bool L_21 = V_0;
		if (!L_21)
		{
			goto IL_004f;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:34>
		return (bool)1;
	}

IL_004f:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:35>
		V_0 = (bool)1;
	}

IL_0051:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:30>
		int32_t L_22 = V_3;
		V_3 = ((int32_t)il2cpp_codegen_add(L_22, 1));
	}

IL_0055:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:30>
		int32_t L_23 = V_3;
		if ((((int32_t)L_23) < ((int32_t)3)))
		{
			goto IL_0013;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:28>
		int32_t L_24 = V_1;
		V_1 = ((int32_t)il2cpp_codegen_add(L_24, 1));
	}

IL_005d:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:28>
		int32_t L_25 = V_1;
		if ((((int32_t)L_25) < ((int32_t)3)))
		{
			goto IL_0006;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:39>
		return (bool)0;
	}
}
// Method Definition Index: 115395
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* Triangle_GetCentroid_mA3FC4743A9681A58A97A61FEA04E4CE2D88C57DD (Triangle_t85225D72A662D1AE165E22F9D410C8B7DA3DAA6F* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	float V_0 = 0.0f;
	float V_1 = 0.0f;
	float V_2 = 0.0f;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:43>
		bool L_0 = __this->___centroIdComputed;
		if (!L_0)
		{
			goto IL_000f;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:44>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_1 = __this->___centroid;
		return L_1;
	}

IL_000f:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:46>
		__this->___centroIdComputed = (bool)1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:47>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_2 = __this->___points;
		NullCheck(L_2);
		int32_t L_3 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_4 = (L_2)->GetAt(static_cast<il2cpp_array_size_t>(L_3));
		NullCheck(L_4);
		float L_5 = L_4->___x;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_6 = __this->___points;
		NullCheck(L_6);
		int32_t L_7 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_8 = (L_6)->GetAt(static_cast<il2cpp_array_size_t>(L_7));
		NullCheck(L_8);
		float L_9 = L_8->___x;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_10 = __this->___points;
		NullCheck(L_10);
		int32_t L_11 = 2;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_12 = (L_10)->GetAt(static_cast<il2cpp_array_size_t>(L_11));
		NullCheck(L_12);
		float L_13 = L_12->___x;
		V_0 = ((float)(((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(L_5, L_9)), L_13))/(3.0f)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:48>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_14 = __this->___points;
		NullCheck(L_14);
		int32_t L_15 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_16 = (L_14)->GetAt(static_cast<il2cpp_array_size_t>(L_15));
		NullCheck(L_16);
		float L_17 = L_16->___y;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_18 = __this->___points;
		NullCheck(L_18);
		int32_t L_19 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_20 = (L_18)->GetAt(static_cast<il2cpp_array_size_t>(L_19));
		NullCheck(L_20);
		float L_21 = L_20->___y;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_22 = __this->___points;
		NullCheck(L_22);
		int32_t L_23 = 2;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_24 = (L_22)->GetAt(static_cast<il2cpp_array_size_t>(L_23));
		NullCheck(L_24);
		float L_25 = L_24->___y;
		V_1 = ((float)(((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(L_17, L_21)), L_25))/(3.0f)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:49>
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_26 = __this->___points;
		NullCheck(L_26);
		int32_t L_27 = 0;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_28 = (L_26)->GetAt(static_cast<il2cpp_array_size_t>(L_27));
		NullCheck(L_28);
		float L_29 = L_28->___z;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_30 = __this->___points;
		NullCheck(L_30);
		int32_t L_31 = 1;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_32 = (L_30)->GetAt(static_cast<il2cpp_array_size_t>(L_31));
		NullCheck(L_32);
		float L_33 = L_32->___z;
		PointU5BU5D_t073C019AF936FA7041F5C60356A85397A0D36FB2* L_34 = __this->___points;
		NullCheck(L_34);
		int32_t L_35 = 2;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_36 = (L_34)->GetAt(static_cast<il2cpp_array_size_t>(L_35));
		NullCheck(L_36);
		float L_37 = L_36->___z;
		V_2 = ((float)(((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(L_29, L_33)), L_37))/(3.0f)));
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:50>
		float L_38 = V_0;
		float L_39 = V_1;
		float L_40 = V_2;
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_41 = (Point_t13126743CEDB2A83E25B6018553E5022E06D2790*)il2cpp_codegen_object_new(Point_t13126743CEDB2A83E25B6018553E5022E06D2790_il2cpp_TypeInfo_var);
		Point__ctor_m3880ABAFFE7200A77D51369E12E08A0EF9974B4F(L_41, L_38, L_39, L_40, NULL);
		__this->___centroid = L_41;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___centroid), (void*)L_41);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/Core/Triangle.cs:51>
		Point_t13126743CEDB2A83E25B6018553E5022E06D2790* L_42 = __this->___centroid;
		return L_42;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
// Method Definition Index: 115396
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereConfig_OnEnable_mB95C2EA5598562B5572BFA63DA8BB77B4FCFF666 (HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Application_tDB03BE91CDF0ACA614A5E0B67CFB77C44EB19B21_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteralAA5D14A3F019134EF42083FAC4AFA3DD9DAF0B04);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:23>
		il2cpp_codegen_runtime_class_init_inline(Application_tDB03BE91CDF0ACA614A5E0B67CFB77C44EB19B21_il2cpp_TypeInfo_var);
		bool L_0;
		L_0 = Application_get_isPlaying_m25B0ABDFEF54F5370CD3F263A813540843D00F34(NULL);
		if (!L_0)
		{
			goto IL_0018;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:24>
		MonoBehaviour_Invoke_mF724350C59362B0F1BFE26383209A274A29A63FB(__this, _stringLiteralAA5D14A3F019134EF42083FAC4AFA3DD9DAF0B04, (0.0f), NULL);
		return;
	}

IL_0018:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:26>
		HexasphereConfig_LoadConfiguration_m64902A2456E137780C2D549C81B698459F6418C8(__this, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:28>
		return;
	}
}
// Method Definition Index: 115397
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereConfig_LoadConfiguration_m64902A2456E137780C2D549C81B698459F6418C8 (HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Component_GetComponent_TisHexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC_m1AB88AA716F1C4F1ED1B562648F98C5330FEC7B3_RuntimeMethod_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Debug_t8394C7EEAECA3689C2C9B9DE9C7166D73596276F_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_il2cpp_TypeInfo_var);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral6586EC4CA6BDC3EEC4B0F6A15908751430DE99EE);
		s_Il2CppMethodInitialized = true;
	}
	Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* V_0 = NULL;
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:35>
		String_t* L_0 = __this->___config;
		if (L_0)
		{
			goto IL_0009;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:36>
		return;
	}

IL_0009:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:38>
		Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* L_1;
		L_1 = Component_GetComponent_TisHexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC_m1AB88AA716F1C4F1ED1B562648F98C5330FEC7B3(__this, Component_GetComponent_TisHexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC_m1AB88AA716F1C4F1ED1B562648F98C5330FEC7B3_RuntimeMethod_var);
		V_0 = L_1;
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:39>
		Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* L_2 = V_0;
		il2cpp_codegen_runtime_class_init_inline(Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C_il2cpp_TypeInfo_var);
		bool L_3;
		L_3 = Object_op_Equality_mB6120F782D83091EF56A198FCEBCF066DB4A9605(L_2, (Object_tC12DECB6760A7F2CBF65D9DCF18D044C2D97152C*)NULL, NULL);
		if (!L_3)
		{
			goto IL_0024;
		}
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:40>
		il2cpp_codegen_runtime_class_init_inline(Debug_t8394C7EEAECA3689C2C9B9DE9C7166D73596276F_il2cpp_TypeInfo_var);
		Debug_Log_m87A9A3C761FF5C43ED8A53B16190A53D08F818BB(_stringLiteral6586EC4CA6BDC3EEC4B0F6A15908751430DE99EE, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:41>
		return;
	}

IL_0024:
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:43>
		Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* L_4 = V_0;
		Texture2DU5BU5D_t05332F1E3F7D4493E304C702201F9BE4F9236191* L_5 = __this->___textures;
		NullCheck(L_4);
		L_4->___textures = L_5;
		Il2CppCodeGenWriteBarrier((void**)(&L_4->___textures), (void*)L_5);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:44>
		Hexasphere_t411FC336E4E84A83268E6209917563C9A08A02EC* L_6 = V_0;
		String_t* L_7 = __this->___config;
		NullCheck(L_6);
		Hexasphere_SetTilesConfigurationData_mE09BDF40221201C6DE3C320634DB5A3412E22FCF(L_6, L_7, NULL);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:45>
		return;
	}
}
// Method Definition Index: 115398
IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR void HexasphereConfig__ctor_m0E5357AFB6C6BB727F62162A68EABB6816B38F34 (HexasphereConfig_t84F9E246A7C30540F8B5CDA73889D43EE71C5E5A* __this, const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral16A31016B4ED8ACC43060D56B4167B4F84B62186);
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&_stringLiteral1DD9A9C5EC5E22754998A64514F4804E700D8942);
		s_Il2CppMethodInitialized = true;
	}
	{
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:10>
		__this->___info = _stringLiteral1DD9A9C5EC5E22754998A64514F4804E700D8942;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___info), (void*)_stringLiteral1DD9A9C5EC5E22754998A64514F4804E700D8942);
		//<source_info:C:/Users/chris/UnityProjects/social-universe/Assets/Plugins/Hexasphere/Scripts/HexasphereConfig.cs:14>
		__this->___title = _stringLiteral16A31016B4ED8ACC43060D56B4167B4F84B62186;
		Il2CppCodeGenWriteBarrier((void**)(&__this->___title), (void*)_stringLiteral16A31016B4ED8ACC43060D56B4167B4F84B62186);
		MonoBehaviour__ctor_m592DB0105CA0BC97AA1C5F4AD27B12D68A3B7C1E(__this, NULL);
		return;
	}
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#pragma clang diagnostic ignored "-Wunused-variable"
#endif
#ifdef __clang__
#pragma clang diagnostic pop
#endif
// Method Definition Index: 63953
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 Vector4_get_zero_m3D61F5FA9483CD9C08977D9D8852FB448B4CE6D1_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 L_0 = ((Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3_StaticFields*)il2cpp_codegen_static_fields_for(Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3_il2cpp_TypeInfo_var))->___zeroVector;
		return L_0;
	}
}
// Method Definition Index: 63964
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector4_op_Implicit_m0217ADDC8CADDB93ACBABB17A50207698DAB0071_inline (Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 ___0_v, const RuntimeMethod* method) 
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		il2cpp_codegen_initobj((&V_0), sizeof(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2));
		Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 L_0 = ___0_v;
		float L_1 = L_0.___x;
		(&V_0)->___x = L_1;
		Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 L_2 = ___0_v;
		float L_3 = L_2.___y;
		(&V_0)->___y = L_3;
		Vector4_t58B63D32F48C0DBF50DE2C60794C4676C80EDBE3 L_4 = ___0_v;
		float L_5 = L_4.___z;
		(&V_0)->___z = L_5;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6 = V_0;
		return L_6;
	}
}
// Method Definition Index: 63725
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_one_mC9B289F1E15C42C597180C9FE6FB492495B51D02_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ((Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_StaticFields*)il2cpp_codegen_static_fields_for(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var))->___oneVector;
		return L_0;
	}
}
// Method Definition Index: 63724
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_zero_m0C1249C3F25B1C70EAD3CC8B31259975A457AE39_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ((Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_StaticFields*)il2cpp_codegen_static_fields_for(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var))->___zeroVector;
		return L_0;
	}
}
// Method Definition Index: 63728
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_get_up_m128AF3FDC820BF59D5DE86D973E7DE3F20C3AEBA_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ((Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_StaticFields*)il2cpp_codegen_static_fields_for(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2_il2cpp_TypeInfo_var))->___upVector;
		return L_0;
	}
}
// Method Definition Index: 63882
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 Vector2_get_one_m9097EB8DC23C26118A591AF16702796C3EF51DFB_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 L_0 = ((Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_StaticFields*)il2cpp_codegen_static_fields_for(Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_il2cpp_TypeInfo_var))->___oneVector;
		return L_0;
	}
}
// Method Definition Index: 63881
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 Vector2_get_zero_m32506C40EC2EE7D5D4410BF40D3EE683A3D5F32C_inline (const RuntimeMethod* method) 
{
	static bool s_Il2CppMethodInitialized;
	if (!s_Il2CppMethodInitialized)
	{
		il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_il2cpp_TypeInfo_var);
		s_Il2CppMethodInitialized = true;
	}
	{
		Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7 L_0 = ((Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_StaticFields*)il2cpp_codegen_static_fields_for(Vector2_t1FD6F485C871E832B347AB2DC8CBA08B739D8DF7_il2cpp_TypeInfo_var))->___zeroVector;
		return L_0;
	}
}
// Method Definition Index: 63584
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Color_tD001788D726C3A7F1379BEED0260B9591F440C1F Color_get_white_m068F5AF879B0FCA584E3693F762EA41BB65532C6_inline (const RuntimeMethod* method) 
{
	{
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_0;
		memset((&L_0), 0, sizeof(L_0));
		Color__ctor_m3786F0D6E510D9CFA544523A955870BD2A514C8C_inline((&L_0), (1.0f), (1.0f), (1.0f), (1.0f), NULL);
		return L_0;
	}
}
// Method Definition Index: 63587
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B Color32_op_Implicit_m79AF5E0BDE9CE041CAC4D89CBFA66E71C6DD1B70_inline (Color_tD001788D726C3A7F1379BEED0260B9591F440C1F ___0_c, const RuntimeMethod* method) 
{
	Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		il2cpp_codegen_initobj((&V_0), sizeof(Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B));
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_0 = ___0_c;
		float L_1 = L_0.___r;
		float L_2;
		L_2 = Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline(L_1, NULL);
		float L_3;
		L_3 = bankers_roundf(((float)il2cpp_codegen_multiply(L_2, (255.0f))));
		(&V_0)->___r = (uint8_t)il2cpp_codegen_cast_floating_point<uint8_t, int32_t, float>(L_3);
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_4 = ___0_c;
		float L_5 = L_4.___g;
		float L_6;
		L_6 = Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline(L_5, NULL);
		float L_7;
		L_7 = bankers_roundf(((float)il2cpp_codegen_multiply(L_6, (255.0f))));
		(&V_0)->___g = (uint8_t)il2cpp_codegen_cast_floating_point<uint8_t, int32_t, float>(L_7);
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_8 = ___0_c;
		float L_9 = L_8.___b;
		float L_10;
		L_10 = Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline(L_9, NULL);
		float L_11;
		L_11 = bankers_roundf(((float)il2cpp_codegen_multiply(L_10, (255.0f))));
		(&V_0)->___b = (uint8_t)il2cpp_codegen_cast_floating_point<uint8_t, int32_t, float>(L_11);
		Color_tD001788D726C3A7F1379BEED0260B9591F440C1F L_12 = ___0_c;
		float L_13 = L_12.___a;
		float L_14;
		L_14 = Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline(L_13, NULL);
		float L_15;
		L_15 = bankers_roundf(((float)il2cpp_codegen_multiply(L_14, (255.0f))));
		(&V_0)->___a = (uint8_t)il2cpp_codegen_cast_floating_point<uint8_t, int32_t, float>(L_15);
		Color32_t73C5004937BF5BB8AD55323D51AAA40A898EF48B L_16 = V_0;
		return L_16;
	}
}
// Method Definition Index: 115050
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Point_t13126743CEDB2A83E25B6018553E5022E06D2790* GetCachedPointDelegate_Invoke_mBF05A2028280C63468764F04E9D3B31611A81D6F_inline (GetCachedPointDelegate_t2E3E2313DE530B246F58CA486B7622E6A2ECD206* __this, Point_t13126743CEDB2A83E25B6018553E5022E06D2790* ___0_point, const RuntimeMethod* method) 
{
	typedef Point_t13126743CEDB2A83E25B6018553E5022E06D2790* (*FunctionPointerType) (RuntimeObject*, Point_t13126743CEDB2A83E25B6018553E5022E06D2790*, const RuntimeMethod*);
	return ((FunctionPointerType)__this->___invoke_impl)((Il2CppObject*)__this->___method_code, ___0_point, reinterpret_cast<RuntimeMethod*>(__this->___method));
}
// Method Definition Index: 63696
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Vector3__ctor_m376936E6B999EF1ECBE57D990A386303E2283DE0_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2* __this, float ___0_x, float ___1_y, float ___2_z, const RuntimeMethod* method) 
{
	{
		float L_0 = ___0_x;
		__this->___x = L_0;
		float L_1 = ___1_y;
		__this->___y = L_1;
		float L_2 = ___2_z;
		__this->___z = L_2;
		return;
	}
}
// Method Definition Index: 62199
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Rect__ctor_m18C3033D135097BEE424AAA68D91C706D2647F23_inline (Rect_tA04E0F8A1830E767F40FB27ECD8D309303571F0D* __this, float ___0_x, float ___1_y, float ___2_width, float ___3_height, const RuntimeMethod* method) 
{
	{
		float L_0 = ___0_x;
		__this->___m_XMin = L_0;
		float L_1 = ___1_y;
		__this->___m_YMin = L_1;
		float L_2 = ___2_width;
		__this->___m_Width = L_2;
		float L_3 = ___3_height;
		__this->___m_Height = L_3;
		return;
	}
}
// Method Definition Index: 63553
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void Color__ctor_m3786F0D6E510D9CFA544523A955870BD2A514C8C_inline (Color_tD001788D726C3A7F1379BEED0260B9591F440C1F* __this, float ___0_r, float ___1_g, float ___2_b, float ___3_a, const RuntimeMethod* method) 
{
	{
		float L_0 = ___0_r;
		__this->___r = L_0;
		float L_1 = ___1_g;
		__this->___g = L_1;
		float L_2 = ___2_b;
		__this->___b = L_2;
		float L_3 = ___3_a;
		__this->___a = L_3;
		return;
	}
}
// Method Definition Index: 63733
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_op_Subtraction_mE42023FF80067CB44A1D4A27EB7CF2B24CABB828_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_a, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_b, const RuntimeMethod* method) 
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		il2cpp_codegen_initobj((&V_0), sizeof(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ___0_a;
		float L_1 = L_0.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2 = ___1_b;
		float L_3 = L_2.___x;
		(&V_0)->___x = ((float)il2cpp_codegen_subtract(L_1, L_3));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4 = ___0_a;
		float L_5 = L_4.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6 = ___1_b;
		float L_7 = L_6.___y;
		(&V_0)->___y = ((float)il2cpp_codegen_subtract(L_5, L_7));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_8 = ___0_a;
		float L_9 = L_8.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_10 = ___1_b;
		float L_11 = L_10.___z;
		(&V_0)->___z = ((float)il2cpp_codegen_subtract(L_9, L_11));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_12 = V_0;
		return L_12;
	}
}
// Method Definition Index: 63699
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 Vector3_Cross_mF93A280558BCE756D13B6CC5DCD7DE8A43148987_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_lhs, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_rhs, const RuntimeMethod* method) 
{
	Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 V_0;
	memset((&V_0), 0, sizeof(V_0));
	{
		il2cpp_codegen_initobj((&V_0), sizeof(Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ___0_lhs;
		float L_1 = L_0.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2 = ___1_rhs;
		float L_3 = L_2.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4 = ___0_lhs;
		float L_5 = L_4.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6 = ___1_rhs;
		float L_7 = L_6.___y;
		(&V_0)->___x = ((float)il2cpp_codegen_subtract(((float)il2cpp_codegen_multiply(L_1, L_3)), ((float)il2cpp_codegen_multiply(L_5, L_7))));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_8 = ___0_lhs;
		float L_9 = L_8.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_10 = ___1_rhs;
		float L_11 = L_10.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_12 = ___0_lhs;
		float L_13 = L_12.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_14 = ___1_rhs;
		float L_15 = L_14.___z;
		(&V_0)->___y = ((float)il2cpp_codegen_subtract(((float)il2cpp_codegen_multiply(L_9, L_11)), ((float)il2cpp_codegen_multiply(L_13, L_15))));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_16 = ___0_lhs;
		float L_17 = L_16.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_18 = ___1_rhs;
		float L_19 = L_18.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_20 = ___0_lhs;
		float L_21 = L_20.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_22 = ___1_rhs;
		float L_23 = L_22.___x;
		(&V_0)->___z = ((float)il2cpp_codegen_subtract(((float)il2cpp_codegen_multiply(L_17, L_19)), ((float)il2cpp_codegen_multiply(L_21, L_23))));
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_24 = V_0;
		return L_24;
	}
}
// Method Definition Index: 63709
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR float Vector3_Dot_mBB86BB940AA0A32FA7D3C02AC42E5BC7095A5D52_inline (Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___0_lhs, Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 ___1_rhs, const RuntimeMethod* method) 
{
	{
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_0 = ___0_lhs;
		float L_1 = L_0.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_2 = ___1_rhs;
		float L_3 = L_2.___x;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_4 = ___0_lhs;
		float L_5 = L_4.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_6 = ___1_rhs;
		float L_7 = L_6.___y;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_8 = ___0_lhs;
		float L_9 = L_8.___z;
		Vector3_t24C512C7B96BBABAD472002D0BA2BDA40A5A80B2 L_10 = ___1_rhs;
		float L_11 = L_10.___z;
		return ((float)il2cpp_codegen_add(((float)il2cpp_codegen_add(((float)il2cpp_codegen_multiply(L_1, L_3)), ((float)il2cpp_codegen_multiply(L_5, L_7)))), ((float)il2cpp_codegen_multiply(L_9, L_11))));
	}
}
// Method Definition Index: 11397
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Add_mEBCF994CC3814631017F46A387B1A192ED6C85C7_gshared_inline (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, RuntimeObject* ___0_item, const RuntimeMethod* method) 
{
	ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* V_0 = NULL;
	int32_t V_1 = 0;
	{
		int32_t L_0 = __this->____version;
		__this->____version = ((int32_t)il2cpp_codegen_add(L_0, 1));
		ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* L_1 = __this->____items;
		V_0 = L_1;
		int32_t L_2 = __this->____size;
		V_1 = L_2;
		int32_t L_3 = V_1;
		ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* L_4 = V_0;
		NullCheck(L_4);
		if ((!(((uint32_t)L_3) < ((uint32_t)((int32_t)(((RuntimeArray*)L_4)->max_length))))))
		{
			goto IL_0034;
		}
	}
	{
		int32_t L_5 = V_1;
		__this->____size = ((int32_t)il2cpp_codegen_add(L_5, 1));
		ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* L_6 = V_0;
		int32_t L_7 = V_1;
		RuntimeObject* L_8 = ___0_item;
		NullCheck(L_6);
		(L_6)->SetAt(static_cast<il2cpp_array_size_t>(L_7), (RuntimeObject*)L_8);
		return;
	}

IL_0034:
	{
		RuntimeObject* L_9 = ___0_item;
		List_1_AddWithResize_m79A9BF770BEF9C06BE40D5401E55E375F2726CC4(__this, L_9, il2cpp_rgctx_method(method->klass->rgctx_data, 14));
		return;
	}
}
// Method Definition Index: 11405
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Clear_mF6795DE5F49C1D0B91D6A0955F448B22970D67A9_gshared_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, const RuntimeMethod* method) 
{
	int32_t V_0 = 0;
	{
		int32_t L_0 = __this->____version;
		__this->____version = ((int32_t)il2cpp_codegen_add(L_0, 1));
		goto IL_0035;
	}

IL_0035:
	{
		__this->____size = 0;
	}

IL_003c:
	{
		return;
	}
}
// Method Definition Index: 11405
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Clear_m16C1F2C61FED5955F10EB36BC1CB2DF34B128994_gshared_inline (List_1_tA239CB83DE5615F348BB0507E45F490F4F7C9A8D* __this, const RuntimeMethod* method) 
{
	int32_t V_0 = 0;
	{
		int32_t L_0 = __this->____version;
		__this->____version = ((int32_t)il2cpp_codegen_add(L_0, 1));
	}
	{
		int32_t L_1 = __this->____size;
		V_0 = L_1;
		__this->____size = 0;
		int32_t L_2 = V_0;
		if ((((int32_t)L_2) <= ((int32_t)0)))
		{
			goto IL_003c;
		}
	}
	{
		ObjectU5BU5D_t8061030B0A12A55D5AD8652A20C922FE99450918* L_3 = __this->____items;
		int32_t L_4 = V_0;
		Array_Clear_m50BAA3751899858B097D3FF2ED31F284703FE5CB((RuntimeArray*)L_3, 0, L_4, NULL);
		return;
	}

IL_003c:
	{
		return;
	}
}
// Method Definition Index: 11397
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR void List_1_Add_m0248A96C5334E9A93E6994B7780478BCD994EA3D_gshared_inline (List_1_t05915E9237850A58106982B7FE4BC5DA4E872E73* __this, int32_t ___0_item, const RuntimeMethod* method) 
{
	Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* V_0 = NULL;
	int32_t V_1 = 0;
	{
		int32_t L_0 = __this->____version;
		__this->____version = ((int32_t)il2cpp_codegen_add(L_0, 1));
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_1 = __this->____items;
		V_0 = L_1;
		int32_t L_2 = __this->____size;
		V_1 = L_2;
		int32_t L_3 = V_1;
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_4 = V_0;
		NullCheck(L_4);
		if ((!(((uint32_t)L_3) < ((uint32_t)((int32_t)(((RuntimeArray*)L_4)->max_length))))))
		{
			goto IL_0034;
		}
	}
	{
		int32_t L_5 = V_1;
		__this->____size = ((int32_t)il2cpp_codegen_add(L_5, 1));
		Int32U5BU5D_t19C97395396A72ECAF310612F0760F165060314C* L_6 = V_0;
		int32_t L_7 = V_1;
		int32_t L_8 = ___0_item;
		NullCheck(L_6);
		(L_6)->SetAt(static_cast<il2cpp_array_size_t>(L_7), (int32_t)L_8);
		return;
	}

IL_0034:
	{
		int32_t L_9 = ___0_item;
		List_1_AddWithResize_m378B392086AAB6F400944FA9839516326B3F7BB8(__this, L_9, il2cpp_rgctx_method(method->klass->rgctx_data, 14));
		return;
	}
}
// Method Definition Index: 63824
IL2CPP_MANAGED_FORCE_INLINE IL2CPP_METHOD_ATTR float Mathf_Clamp01_mA7E048DBDA832D399A581BE4D6DED9FA44CE0F14_inline (float ___0_value, const RuntimeMethod* method) 
{
	bool V_0 = false;
	float V_1 = 0.0f;
	bool V_2 = false;
	{
		float L_0 = ___0_value;
		V_0 = (bool)((((float)L_0) < ((float)(0.0f)))? 1 : 0);
		bool L_1 = V_0;
		if (!L_1)
		{
			goto IL_0015;
		}
	}
	{
		V_1 = (0.0f);
		goto IL_002d;
	}

IL_0015:
	{
		float L_2 = ___0_value;
		V_2 = (bool)((((float)L_2) > ((float)(1.0f)))? 1 : 0);
		bool L_3 = V_2;
		if (!L_3)
		{
			goto IL_0029;
		}
	}
	{
		V_1 = (1.0f);
		goto IL_002d;
	}

IL_0029:
	{
		float L_4 = ___0_value;
		V_1 = L_4;
		goto IL_002d;
	}

IL_002d:
	{
		float L_5 = V_1;
		return L_5;
	}
}
