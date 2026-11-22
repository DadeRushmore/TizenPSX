using System;

partial class MenuChecks {
 static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
 internal static void Run(){
  var menu=new MenuNavigation();
  Need(menu.Update(true,1,0,0,true,0)==null,"exit/held Cross cannot reopen game");
  Need(menu.Update(true,0,0,0,true,1)==null,"PS must release before rearming");
  menu.Update(true,0,0,0,false,2);menu.Update(true,0,0,0,false,2.2);
  Need(menu.Update(true,128,0,0,false,2.3)=="Right","D-pad works after game exit");
  Need(menu.Update(true,128,0,0,false,2.4)==null,"direction has initial repeat delay");
  Need(menu.Update(true,128,0,0,false,2.71)=="Right","direction repeat works");
  Need(menu.Update(true,1,0,0,false,2.8)=="Return","Cross selects after navigation");
  Need(menu.Update(true,1,0,0,false,4)==null,"held confirmation cannot activate next screen");
  menu.Update(true,0,0,0,false,4.1);menu.Update(true,0,0,0,false,4.3);
  Need(menu.Update(true,256,0,0,false,4.4)=="XF86Back","Circle returns from settings");
  menu.Update(true,0,0,0,false,5);menu.Update(true,0,0,0,false,5.2);
  Need(menu.Update(true,0,-1,0,false,5.3)=="Left","left stick navigates regardless of PS1 analog mode");
  menu.Update(false,0,0,0,false,5.4);
  Need(menu.Update(true,8,0,0,false,6)==null,"reconnect held Start cannot launch");
  menu.Update(true,0,0,0,false,6.1);menu.Update(true,0,0,0,false,6.3);
  Need(menu.Update(true,8,0,0,false,6.4)=="Return","Start confirms on fresh press");
  menu.Reset();Need(menu.Update(true,64,0,0,false,8)==null,"another game exit resets menu guard");
  menu.Update(true,0,0,0,false,9);menu.Update(true,0,0,0,false,9.2);
  Need(menu.Update(true,2,0,0,false,9.3)=="p","Square opens performance shortcut on fresh press");
  var filter=new MenuKeyFilter();
  Need(filter.Accept("Return",false,1)&&!filter.Accept("Return",true,1.02),"key-first duplicate filtered");
  Need(filter.Accept("Down",true,2)&&!filter.Accept("Down",false,2.02),"native-first duplicate filtered");
  Need(filter.Accept("XF86Back",false,2.03),"different remote command remains available");
  Need(filter.Accept("Down",false,3)&&filter.Accept("Down",false,3.01),"ordinary remote repeats retained");
  Need(filter.Accept("Return",true,4)&&!filter.Accept("KP_Enter",false,4.02),"Enter aliases deduplicated");
  Need(InputPolling.Allowed(false,false,false,false,true,true,true),"initialized menus continue polling");
  Need(!InputPolling.Allowed(true,false,false,false,true,true,true),"paused menus never poll");
  Need(!InputPolling.Allowed(false,false,true,false,true,true,true),"key tester still never polls");
  Need(!InputPolling.Allowed(false,false,false,false,false,true,true)&&!InputPolling.Allowed(false,false,false,false,true,false,true),"polling-off and key-only respected");
  Need(!InputPolling.Allowed(false,false,false,false,true,true,false),"uninitialized menu does not open native driver");
  Console.WriteLine("PASS: menu return/release guard, repeat, confirmation, disconnect, duplicate keys and polling policy.");
 }
}
