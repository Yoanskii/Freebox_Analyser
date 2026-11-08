using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.App;
public sealed class ProbeChart : FrameworkElement
{
    public static readonly DependencyProperty ProbesProperty=DependencyProperty.Register(nameof(Probes),typeof(List<Probe>),typeof(ProbeChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public List<Probe>? Probes {get=>(List<Probe>?)GetValue(ProbesProperty);set=>SetValue(ProbesProperty,value);}
    double minutes=2;Probe[] visible=[];double max=1;DateTimeOffset end;
    readonly Brush cyan=new SolidColorBrush(Color.FromRgb(80,223,210));
    public ProbeChart(){MouseWheel+=(_,e)=>{minutes=Math.Clamp(minutes*(e.Delta>0?.5:2),.25,15);InvalidateVisual();e.Handled=true;};MouseMove+=Hover;KeyDown+=(_,e)=>{if(e.Key is Key.Add or Key.OemPlus){minutes=Math.Max(.25,minutes/2);InvalidateVisual();}if(e.Key is Key.Subtract or Key.OemMinus){minutes=Math.Min(15,minutes*2);InvalidateVisual();}};}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight));if(ActualWidth<150||ActualHeight<100)return;
        end=DateTimeOffset.UtcNow;visible=(Probes??[]).Where(p=>p.At>=end.AddMinutes(-minutes)).ToArray();max=Math.Max(1,visible.MaxBy(p=>p.RttMs)?.RttMs??1);
        double w=ActualWidth-85,h=ActualHeight-65;var pen=new Pen(new SolidColorBrush(Color.FromRgb(75,96,119)),1);
        void Text(string text,double x,double y,Brush? brush=null)=>dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("fr-FR"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,brush??Brushes.LightSteelBlue,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        double X(Probe p)=>60+w*(p.At-end.AddMinutes(-minutes)).TotalMinutes/minutes;
        double Y(double rtt)=>25+h*(1-rtt/max);
        for(int i=0;i<4;i++){double y=25+h*i/3;dc.DrawLine(pen,new Point(60,y),new Point(60+w,y));Text($"{max*(3-i)/3:F0}",5,y-7);}
        Text("ms",5,3);Text($"{end.AddMinutes(-minutes).ToLocalTime():HH:mm:ss}",60,25+h+25);Text($"{end.ToLocalTime():HH:mm:ss}",Math.Max(65,60+w-65),25+h+25);Text($"● RTT   | Échec ICMP   ▪ Invalide   · fenêtre {minutes:F2} min",Math.Max(80,w/2-120),3,cyan);
        Probe? previous=null;
        foreach(var bucket in visible.GroupBy(p=>(int)X(p)))
        {
            foreach(var p in bucket.Where(p=>p.RttMs.HasValue).OrderBy(p=>p.RttMs).TakeLast(1))
            {
                var point=new Point(X(p),Y(p.RttMs!.Value));
                if(previous?.RttMs is double rtt && p.At-previous.At<TimeSpan.FromSeconds(5) && previous.Address==p.Address && previous.InterfaceId==p.InterfaceId)dc.DrawLine(new Pen(cyan,1.5),new Point(X(previous),Y(rtt)),point);
                dc.DrawEllipse(cyan,null,point,2,2);previous=p;
            }
            if(bucket.Any(p=>p.Eligible&&p.Outcome!=ProbeOutcome.Success)){dc.DrawLine(new Pen(Brushes.LightCoral,2),new Point(bucket.Key,25+h+3),new Point(bucket.Key,25+h+13));previous=null;}
            if(bucket.Any(p=>!p.Eligible)){dc.DrawRectangle(Brushes.Gray,null,new Rect(bucket.Key,25+h+7,3,3));previous=null;}
        }
        if(visible.Length==0)Text("Aucune mesure disponible dans cette fenêtre.",80,70);
    }
    void Hover(object sender,MouseEventArgs e)
    {
        if(visible.Length==0||ActualWidth<150)return;var point=e.GetPosition(this);var at=end.AddMinutes(-minutes+minutes*(point.X-60)/(ActualWidth-85));var p=visible.MinBy(p=>Math.Abs((p.At-at).TotalSeconds));
        if(p is not null)ToolTip=$"{p.At.ToLocalTime():HH:mm:ss.fff} · {p.Address}\n{p.Outcome} · {(p.RttMs.HasValue?$"{p.RttMs:F2} ms":"RTT absent")} · {p.Detail}";
    }
}
