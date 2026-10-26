using PokemonRebornLauncher.Services;
int checks=0;
void Check(bool value,string name) { if(!value)throw new Exception(name);checks++; }
void Advance(EventPropSimulation s,double t,bool reduced=false) {for(int i=0;i<(int)Math.Round(t/.025);i++)s.Step(.025,reduced);}
foreach(bool reduced in new[]{false,true})
{
    var s=new EventPropSimulation(31);s.Reset("halloween");var p=s.Props.Single(p=>p.Kind==PropKind.Bat);
    double x=p.X,y=p.Y,scale=p.Scale;int sounds=0;s.SoundRequested+=_=>sounds++;
    Check(s.Activate(p.Id),"First click activates");Check(!s.Activate(p.Id)&&sounds==1,"No spam activation/sound");
    Advance(s,.1,reduced);Check(p.BatPhase==BatFlightPhase.Anticipation&&p.X==x&&p.Y==y,"Anticipation stays on roof");
    Advance(s,.3,reduced);
    if(reduced) Check(p.BatPhase==BatFlightPhase.Away&&p.Opacity==0&&p.X==x&&p.Y==y,"Reduced motion fades in place");
    else
    {
        Check(p.BatPhase==BatFlightPhase.Flying&&p.Y<y,"Takeoff");
        Advance(s,1);Check(p.Y<y-15&&p.Scale<scale&&p.Rotation!=0,"Curve banks and recedes");
        Advance(s,1.5);Check(p.Opacity is >0 and <1,"Soft fade before departure");
    }
    Advance(s,reduced?3: .5,reduced);Check(p.BatPhase==BatFlightPhase.Away&&p.Opacity==0&&!s.Activate(p.Id),"Away means invisible/cooldown");
    Check(p.RespawnAfter is >=11.2 and <=18.2,"8–15 seconds away after 3.2 second flight");
    double untilReturn=p.RespawnAfter-p.SinceActivation;
    Advance(s,Math.Max(0,untilReturn)+.25,reduced);
    Check(p.BatPhase==BatFlightPhase.Returning&&p.Opacity is >0 and <1&&p.X==x&&p.Y==y,"Smooth return at exact roof anchor");
    Check(!s.Activate(p.Id),"Returning cannot be spammed");
    Advance(s,.6,reduced);Check(p.State==PropState.Idle&&p.BatPhase==BatFlightPhase.Perched&&p.Opacity==1,"Fully perched again");
    Check(s.Activate(p.Id),"Second flight is available after cooldown");
    Advance(s,.6,reduced);s.CancelBatFlights();
    Check(p.State==PropState.Idle&&p.X==x&&p.Y==y&&p.Opacity==1,"Disable cancels and resets flight");
    int id=p.Id;s.Reset("summer");Check(!s.Activate(id)&&s.Props.All(p=>p.Kind!=PropKind.Bat),"Theme reset cancels old IDs");
    foreach(var a in s.Props)Check(double.IsFinite(a.X+a.Y+a.Scale+a.Rotation)&&a.X is >=0 and <=1000&&a.Y is >=0 and <=400,"Finite world bounds");
}
Console.WriteLine($"BAT_FLIGHT_OK {checks} checks");
