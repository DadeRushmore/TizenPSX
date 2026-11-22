internal static class MappingRemotePolicy {
 internal static bool IsNavigation(string key)=>key=="Left"||key=="Right"||key=="Up"||key=="Down"||key=="Return"||key=="KP_Enter"||key=="XF86Back"||key=="Escape";
 // Delay window navigation until native controller echoes can be rejected.
 internal static bool Accept(double keyTime,double lastControllerInput,bool neutral)=>neutral&&lastControllerInput<keyTime-.2;
}
