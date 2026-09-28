using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class P {
  static void Main() {
    string d = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    string log = Path.Combine(d, "fix_asm.log");
    File.WriteAllText(log, "=== fix asm " + DateTime.Now.ToString("o") + " ===\r\n");
    Action<string> L = m => { Console.WriteLine(m); File.AppendAllText(log, m + "\r\n"); };

    const double PlateT = 0.0008;
    const double BossH = 0.0015;
    const double ShaftL1 = 0.017;
    const double PulleyLen = 0.006;
    const double TipPast = 0.002;
    double tip = PlateT + BossH + ShaftL1; // 0.0193
    double pz = tip - TipPast - PulleyLen; // 0.0113

    SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
    sw.Visible = true;
    sw.CloseAllDocuments(true);
    System.Threading.Thread.Sleep(500);

    string tpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Assembly.ASMDOT";
    object adoc = sw.NewDocument(tpl, 0, 0, 0);
    AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
    ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;
    int e=0,w=0;

    Func<string,double,double,double,Component2> add = (path, x, y, z) => {
      sw.OpenDoc6(path, 1, 1, "", ref e, ref w);
      sw.ActivateDoc3(md.GetTitle(), false, 0, ref e);
      Component2 c = null;
      try { c = asm.AddComponent5(path, 0, "", false, "", x, y, z); } catch {}
      if (c == null) try { c = asm.AddComponent4(path, "", x, y, z); } catch {}
      if (c == null) {
        asm.AddComponent(path, x, y, z);
        object[] comps = (object[])asm.GetComponents(false);
        if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
      }
      if (c == null) throw new Exception("add fail " + path);
      // force transform to exact XYZ (identity rotation)
      MathTransform t = c.Transform2;
      double[] a = (double[])t.ArrayData;
      a[0]=1; a[1]=0; a[2]=0;
      a[3]=0; a[4]=1; a[5]=0;
      a[6]=0; a[7]=0; a[8]=1;
      a[9]=x; a[10]=y; a[11]=z;
      a[12]=1; // scale
      t.ArrayData = a;
      c.Transform2 = t;
      double[] r = (double[])c.Transform2.ArrayData;
      L(string.Format("placed {0} at ({1:F4},{2:F4},{3:F4}) mm",
        Path.GetFileName(path), r[9]*1000, r[10]*1000, r[11]*1000));
      return c;
    };

    string motor = Path.Combine(d, "Motor_PM42L_048_EPAO.SLDPRT");
    string pulley = Path.Combine(d, "Pulley_GT2_12T.SLDPRT");
    Component2 cm = add(motor, 0, 0, 0);
    try {
      md.Extension.SelectByID2(cm.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
      asm.FixComponent();
    } catch {}
    Component2 cp = add(pulley, 0, 0, pz);
    try { cp.Select4(false, null, false); asm.UnfixComponent(); } catch {}

    md.EditRebuild3();
    L(string.Format("tip={0:F2} pulley={1:F2}..{2:F2}", tip*1000, pz*1000, (pz+PulleyLen)*1000));

    // verify
    object[] all = (object[])asm.GetComponents(true);
    if (all != null) {
      foreach (object oc in all) {
        Component2 c = (Component2)oc;
        double[] r = (double[])c.Transform2.ArrayData;
        L(string.Format("verify {0} tz={1:F4} mm", c.Name2, r[11]*1000));
      }
    }

    string tmp = Path.Combine(d, "Motor_PM42L_048_EPAO_Pulley_new.SLDASM");
    string dest = Path.Combine(d, "Motor_PM42L_048_EPAO_Pulley.SLDASM");
    int ee=0,ww=0;
    bool ok = md.Extension.SaveAs(tmp, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
      (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref ee, ref ww);
    L("SaveAs tmp ok=" + ok + " e=" + ee);
    sw.CloseDoc(md.GetTitle());
    if (File.Exists(dest)) {
      try { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); } catch (Exception ex) { L("del dest: " + ex.Message); }
    }
    File.Move(tmp, dest);
    L("Assembly fixed -> " + dest);
  }
}
