// Test-only runtime: exercises reflection/ref arguments and queue handling.
// It does not claim to reproduce firmware return-code semantics or latency.
using System.Collections.Generic;
using System.Numerics;
namespace Tizen.TV.Accessory {
 enum PlayerIndex {One,Two}
 enum Buttons {None=0}
 struct GamePadState {
  internal uint Bits; internal Vector2 Left,Right;internal float LT,RT;
  public GamePadState(Vector2 left,Vector2 right,float lt,float rt,Buttons buttons){Bits=(uint)buttons;Left=left;Right=right;LT=lt;RT=rt;}
 }
 struct GamePadCapabilities {public bool IsConnected;}
 static class GamePad {
  internal static bool Connected=true;
  static bool Initialise()=>true;
  public static GamePadCapabilities GetCapabilities(PlayerIndex slot)=>new GamePadCapabilities{IsConnected=Connected};
 }
}
static class Interop {
 internal static class GamepadClient {
  internal struct ControllerEvent {public int type,code,value;}
  internal static readonly Queue<ControllerEvent> Queue=new Queue<ControllerEvent>();
  internal static int Result,EmptyDelayMs;
  static int gamepad_get_input_event(int slot,ref ControllerEvent ev){if(Queue.Count==0&&EmptyDelayMs>0)System.Threading.Thread.Sleep(EmptyDelayMs);ev=Queue.Count>0?Queue.Dequeue():default(ControllerEvent);return Result;}
  internal static void Add(int type,int code,int value)=>Queue.Enqueue(new ControllerEvent{type=type,code=code,value=value});
 }
}
