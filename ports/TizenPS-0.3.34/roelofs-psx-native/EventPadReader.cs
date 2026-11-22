using System;
using System.Diagnostics;
using System.Reflection;
using System.Numerics;
using System.Text;
using Tizen.TV.Accessory;

// Experimental firmware adapter. Reuse the installed SDK's exact native
// declaration and lifecycle; never guess a P/Invoke layout from desktop refs.
internal sealed class EventPadReader {
 readonly ControllerEvents[] states={new ControllerEvents(),new ControllerEvents()};
 MethodInfo initialise,read;
 Type eventType;
 FieldInfo type,code,value;
 internal Action<int,int,int> ButtonEvent;
 internal Action<int,int,int,int> RawEventReceived;
 readonly PadMapping[] customStates=new PadMapping[2];
 PadMapping mapping;
 internal PadMapping Mapping {get=>mapping;set{mapping=value;for(int i=0;i<2;i++)customStates[i]=PadMapping.Parse(value.Serialize());Reset();}}
 bool ready;
 internal bool Initialized=>ready;
 readonly uint[] presses=new uint[2];
 internal GamePadState Pressed(int slot)=>new GamePadState(Vector2.Zero,Vector2.Zero,0,0,(Buttons)presses[slot]);
 ControllerProfile profile;
 internal ControllerProfile Profile {get=>profile;set{if(value!=ControllerProfile.LegacyDigital&&value!=ControllerProfile.UsbPs5)throw new ArgumentOutOfRangeException(nameof(value));if(profile!=value){Reset();profile=value;}}}

 struct RawEvent {internal int Result,Type,Code,Value;internal double Ms;}
 readonly RawEvent[] batch=new RawEvent[128];
 internal string BatchStop="not started";
 internal int CountLimits,TimeLimits,DuplicateDowns;
 void FormatBatch(int count,bool capture){
  if(!capture)return;
  var text=new StringBuilder();
  for(int i=0;i<count;i++){var e=batch[i];text.Append("[").Append(e.Ms.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)).Append("ms rc=").Append(e.Result).Append(" event=").Append(e.Type).Append('/').Append(e.Code).Append('/').Append(e.Value).Append("] ");}
  BatchEvents=text.ToString();
 }

 internal int LimitBatches,EmptyReads,UnknownEvents,UnmatchedReleases;
 readonly bool[] rawHeld=new bool[256];
 readonly int[] downs=new int[256],ups=new int[256];
 internal string ButtonCounts {get{var s=new StringBuilder();for(int i=0;i<256;i++)if(downs[i]+ups[i]>0)s.Append("code ").Append(i).Append(": down=").Append(downs[i]).Append(" up=").Append(ups[i]).Append(" held=").Append(rawHeld[i]).Append("; ");return s.ToString();}}
 internal string Detail="Not started";
 internal string BatchEvents="",LastButtonEvent="none";
 internal void Reset(){CountLimits=TimeLimits=DuplicateDowns=0;BatchStop="reset";LimitBatches=EmptyReads=UnknownEvents=UnmatchedReleases=0;Array.Clear(rawHeld,0,256);Array.Clear(downs,0,256);Array.Clear(ups,0,256);foreach(var state in states)state.Clear();foreach(var map in customStates)map?.Reset();BatchEvents="";LastButtonEvent="none";}
 void Bind(){
  if(read!=null)return;
  const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  initialise=typeof(GamePad).GetMethod("Initialise",flags,null,Type.EmptyTypes,null);
  var interop=typeof(GamePad).Assembly.GetType("Interop+GamepadClient",true);
  var candidate=interop.GetMethod("gamepad_get_input_event",flags);
  if(initialise==null||initialise.ReturnType!=typeof(bool)||candidate==null||candidate.ReturnType!=typeof(int))throw new NotSupportedException("Unrecognized controller runtime methods");
  var parameters=candidate.GetParameters();
  if(parameters.Length!=2||parameters[0].ParameterType!=typeof(int)||!parameters[1].ParameterType.IsByRef)throw new NotSupportedException("Unrecognized controller event signature");
  eventType=parameters[1].ParameterType.GetElementType();
  const BindingFlags fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  type=eventType.GetField("type",fields);code=eventType.GetField("code",fields);value=eventType.GetField("value",fields);
  if(!eventType.IsValueType||type?.FieldType!=typeof(int)||code?.FieldType!=typeof(int)||value?.FieldType!=typeof(int))throw new NotSupportedException("Unrecognized controller event fields");
  read=candidate;
 }
 internal GamePadState Read(int slot,bool capture=false){
  if(slot<0||slot>=states.Length)throw new ArgumentOutOfRangeException(nameof(slot));
  var state=states[slot];presses[slot]=0;
  BatchEvents="";
  int captured=0;
  try{
   Bind();
   if(!ready)ready=(bool)initialise.Invoke(null,null);
   if(!ready||!GamePad.GetCapabilities((PlayerIndex)slot).IsConnected){state.Clear();customStates[slot]?.Reset();Detail="Disconnected / initialization pending";return default(GamePadState);}
   var timer=Stopwatch.StartNew();int count=0;BatchStop="reading";
   do{
    // Fresh zeroed struct prevents stale data being mistaken for another event.
    object[] args={slot,Activator.CreateInstance(eventType)};
    int result=(int)read.Invoke(null,args);
    int t=(int)type.GetValue(args[1]),c=(int)code.GetValue(args[1]),v=(int)value.GetValue(args[1]);
    // Keep the drain hot: defer raw-event string formatting until after polling.
    if(t!=0||result!=0)batch[captured++]=new RawEvent{Result=result,Type=t,Code=c,Value=v,Ms=timer.Elapsed.TotalMilliseconds};
    if(t==1)LastButtonEvent="code="+c+" value="+v+" rc="+result;
    if(result<0){BatchStop="error";throw new InvalidOperationException("Controller read failed: rc="+result);}
    if(t==0&&result==0){EmptyReads++;BatchStop="empty";break;}
    RawEventReceived?.Invoke(slot,t,c,v);
    if(t==1)ButtonEvent?.Invoke(slot,c,v);
    customStates[slot]?.Observe(t,c,v);
    if(capture&&t==1&&c>=0&&c<256){if(v==1){if(rawHeld[c])DuplicateDowns++;downs[c]++;rawHeld[c]=true;}else if(v==0){ups[c]++;if(!rawHeld[c])UnmatchedReleases++;rawHeld[c]=false;}}
    uint before=state.Buttons;
    if(!state.Apply(t,c,v,Profile==ControllerProfile.UsbPs5))UnknownEvents++;
    presses[slot]|=state.Buttons&~before;
    count++;
   }while(count<batch.Length&&timer.Elapsed.TotalMilliseconds<8);
   if(BatchStop=="reading"){
    LimitBatches++;
    if(count>=batch.Length){CountLimits++;BatchStop="count limit";}
    else{TimeLimits++;BatchStop="time limit";}
   }
   FormatBatch(captured,capture);
   if(capture){Detail="stop="+BatchStop+" countLimits="+CountLimits+" timeLimits="+TimeLimits+" duplicateDown="+DuplicateDowns;

   Detail+=" | limits="+LimitBatches+" empty="+EmptyReads+" unknown="+UnknownEvents+" unmatchedUp="+UnmatchedReleases+" | batch="+count+" total="+state.Events+" | raw axes="+string.Join(",",state.Axes)+" | LX/LY="+state.Axes[0]+","+state.Axes[1]+" RX/RY="+state.Axes[2]+","+state.Axes[5]+" L2/R2="+state.Axes[3]+","+state.Axes[4]+" | profile="+Profile+" seen="+string.Join(",",state.AxisSeen);
   }
   bool analog=Profile==ControllerProfile.UsbPs5;
   return new GamePadState(analog?new Vector2(state.Stick(0),state.Stick(1,true)):Vector2.Zero,analog?new Vector2(state.Stick(2),state.Stick(5,true)):Vector2.Zero,mapping!=null&&mapping.Enabled?((customStates[slot].Bits&4096)!=0?1f:0f):analog?state.Trigger(3):0,mapping!=null&&mapping.Enabled?((customStates[slot].Bits&8192)!=0?1f:0f):analog?state.Trigger(4):0,(Buttons)(mapping!=null&&mapping.Enabled?customStates[slot].SdkBits:state.Buttons));
  }catch(TargetInvocationException e){FormatBatch(captured,capture);state.Clear();customStates[slot]?.Reset();throw new InvalidOperationException("Controller runtime: "+(e.InnerException?.Message??e.Message),e);}
  catch{FormatBatch(captured,capture);state.Clear();customStates[slot]?.Reset();throw;}
 }
}
