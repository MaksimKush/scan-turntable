// Sync assemblies to USER-OWNED Motor_PM42L_048_EPAO.SLDPRT.
// NEVER rebuild or overwrite the motor part — inspect bbox/shaft and remate only.
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Program
{
    static string OutDir = @"C:\Users\iprom\Cursor AI\scan-turntable\cad";
    static string PartTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Part.PRTDOT";
    static string AsmTpl = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\Assembly.ASMDOT";
    static string LogPath = Path.Combine(OutDir, "sync_user_motor.log");

    const double PulleyLen = 0.006; // GT2 L=6
    const double TipPast = 0.002;   // tip +2 mm past pulley end
    const double BaseT = 0.008;
    const double HubH = 0.016;
    const double YokeBaseL = 0.210;
    const double ArmT = 0.010;
    const double ArmInset = 0.005;
    const double TiltAxisZ = 0.075;
    const double PlatformOd = 0.200;
    const double PlatformT = 0.006;
    const double TiltShaftLen = 0.220;

    // Measured from user motor (filled by InspectMotor)
    static double TipZ;      // shaft tip Z in part coords (m)
    static double ZMin, ZMax, Dx, Dy, Dz;
    static double MountSpanY; // approx mount ear span along Y

    static void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + m;
        File.AppendAllText(LogPath, line + System.Environment.NewLine);
        Console.WriteLine(line);
    }

    static void SaveDoc(ModelDoc2 model, string path)
    {
        int e = 0, w = 0;
        bool ok = model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e, ref w);
        Log("SaveAs " + Path.GetFileName(path) + " ok=" + ok + " e=" + e);
        if (!ok) throw new Exception("SaveAs failed " + path);
    }

    static void ReplaceFile(string tmp, string dest)
    {
        if (File.Exists(dest))
        {
            File.SetAttributes(dest, FileAttributes.Normal);
            File.Delete(dest);
        }
        File.Move(tmp, dest);
        Log("Replaced " + Path.GetFileName(dest));
    }

    static void InspectMotor(SldWorks sw)
    {
        string path = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        if (!File.Exists(path)) throw new Exception("User motor missing: " + path);
        int e = 0, w = 0;
        ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref e, ref w);
        if (m == null) throw new Exception("Open motor fail e=" + e);
        Log("--- USER Motor_PM42L_048_EPAO (READ-ONLY inspect) ---");
        Log("File time: " + File.GetLastWriteTime(path).ToString("o"));

        PartDoc part = (PartDoc)m;
        object[] bodies = (object[])part.GetBodies2((int)swBodyType_e.swSolidBody, true);
        double[] box = null;
        if (bodies != null)
        {
            foreach (object ob in bodies)
            {
                Body2 b = (Body2)ob;
                double[] bb = (double[])b.GetBodyBox();
                Log(string.Format("  body X[{0:F2}..{1:F2}] Y[{2:F2}..{3:F2}] Z[{4:F2}..{5:F2}] mm",
                    bb[0] * 1000, bb[3] * 1000, bb[1] * 1000, bb[4] * 1000, bb[2] * 1000, bb[5] * 1000));
                if (box == null) box = (double[])bb.Clone();
                else
                {
                    for (int i = 0; i < 3; i++) if (bb[i] < box[i]) box[i] = bb[i];
                    for (int i = 3; i < 6; i++) if (bb[i] > box[i]) box[i] = bb[i];
                }
            }
        }
        if (box == null) throw new Exception("No solid bodies on user motor");
        ZMin = box[2]; ZMax = box[5];
        Dx = (box[3] - box[0]) * 1000;
        Dy = (box[4] - box[1]) * 1000;
        Dz = (box[5] - box[2]) * 1000;
        MountSpanY = (box[4] - box[1]); // m
        // Convention: Front-plane stack, shaft along +Z → tip = ZMax
        TipZ = ZMax;
        Log(string.Format("  UNION Dx={0:F2} Dy={1:F2} Dz={2:F2} mm", Dx, Dy, Dz));
        Log(string.Format("  Z[{0:F2}..{1:F2}] tipZ={2:F2} mm (shaft tip assumed ZMax)", ZMin * 1000, ZMax * 1000, TipZ * 1000));
        Log(string.Format("  mountSpanY={0:F2} mm (ears/body)", MountSpanY * 1000));

        Feature f = (Feature)m.FirstFeature();
        int n = 0;
        while (f != null && n < 80)
        {
            string tn = f.GetTypeName2();
            if (tn.IndexOf("Extrus") >= 0 || tn.IndexOf("Cut") >= 0 || tn.IndexOf("Hole") >= 0 ||
                tn.IndexOf("Pattern") >= 0 || tn.IndexOf("Boss") >= 0 || tn == "RefPlane")
                Log("  feat " + f.Name + " [" + tn + "]");
            f = (Feature)f.GetNextFeature();
            n++;
        }

        // Do NOT save / rebuild motor — close without saving
        sw.CloseDoc(m.GetTitle());
        Log("Motor preserved (closed without save)");
    }

    static void SetTransform(Component2 c, double[] n, double x, double y, double z)
    {
        MathTransform t = c.Transform2;
        double[] a = (double[])t.ArrayData;
        for (int i = 0; i < 9; i++) a[i] = n[i];
        a[9] = x; a[10] = y; a[11] = z; a[12] = 1;
        t.ArrayData = a;
        c.Transform2 = t;
    }

    static Component2 AddComp(SldWorks sw, AssemblyDoc assy, ModelDoc2 md, string path, double x, double y, double z)
    {
        int err = 0, warn = 0;
        sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART, 1, "", ref err, ref warn);
        sw.ActivateDoc3(md.GetTitle(), false, 0, ref err);
        Component2 c = null;
        try { c = assy.AddComponent5(path, 0, "", false, "", x, y, z); } catch { }
        if (c == null) try { c = assy.AddComponent4(path, "", x, y, z); } catch { }
        if (c == null && assy.AddComponent(path, x, y, z))
        {
            object[] comps = (object[])assy.GetComponents(false);
            if (comps != null && comps.Length > 0) c = (Component2)comps[comps.Length - 1];
        }
        if (c == null) throw new Exception("Add fail " + path);
        SetTransform(c, new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }, x, y, z);
        Log(string.Format("  place {0} @ ({1:F2},{2:F2},{3:F2}) mm", Path.GetFileName(path), x * 1000, y * 1000, z * 1000));
        return c;
    }

    static void BuildPulleyAsm(SldWorks sw)
    {
        Log("--- Motor_PM42L_048_EPAO_Pulley.SLDASM ---");
        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pulley = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        double pz = TipZ - TipPast - PulleyLen;

        sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;

        Component2 cm = AddComp(sw, asm, md, motor, 0, 0, 0);
        try
        {
            md.Extension.SelectByID2(cm.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            asm.FixComponent();
        }
        catch { }
        AddComp(sw, asm, md, pulley, 0, 0, pz);
        Log(string.Format("  tip={0:F2} pulley Z={1:F2}..{2:F2} past=+{3:F2}",
            TipZ * 1000, pz * 1000, (pz + PulleyLen) * 1000, TipPast * 1000));

        md.EditRebuild3();
        string tmp = Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley_new.SLDASM");
        SaveDoc(md, tmp);
        sw.CloseDoc(md.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley.SLDASM"));
    }

    static void BuildTurntable(SldWorks sw)
    {
        Log("--- Turntable_Revopoint2Motor.SLDASM (2 motors) ---");
        string motor = Path.Combine(OutDir, "Motor_PM42L_048_EPAO.SLDPRT");
        string pulley = Path.Combine(OutDir, "Pulley_GT2_12T.SLDPRT");
        string baseP = Path.Combine(OutDir, "Base_Revopoint.SLDPRT");
        string hubP = Path.Combine(OutDir, "Hub_Azimuth.SLDPRT");
        string yokeBase = Path.Combine(OutDir, "Yoke_Base.SLDPRT");
        string armL = Path.Combine(OutDir, "Yoke_Arm_L.SLDPRT");
        string armR = Path.Combine(OutDir, "Yoke_Arm_R.SLDPRT");
        string platP = Path.Combine(OutDir, "Platform_Tilt.SLDPRT");
        string shaftP = Path.Combine(OutDir, "Shaft_Tilt.SLDPRT");
        string clampP = Path.Combine(OutDir, "Clamp_TiltShaft.SLDPRT");

        foreach (string p in new[] { motor, pulley, baseP, hubP, yokeBase, armL, armR, platP, shaftP, clampP })
            if (!File.Exists(p)) throw new Exception("Missing " + Path.GetFileName(p));

        sw.NewDocument(AsmTpl, 0, 0, 0);
        AssemblyDoc asm = (AssemblyDoc)sw.ActiveDoc;
        ModelDoc2 md = (ModelDoc2)sw.ActiveDoc;

        Component2 cBase = AddComp(sw, asm, md, baseP, 0, 0, 0);
        try { md.Extension.SelectByID2(cBase.Name2, "COMPONENT", 0, 0, 0, false, 0, null, 0); asm.FixComponent(); } catch { }

        double zM1 = BaseT;
        AddComp(sw, asm, md, motor, 0, 0, zM1);

        double tip1 = zM1 + TipZ;
        double zP1 = tip1 - TipPast - PulleyLen;
        AddComp(sw, asm, md, pulley, 0, 0, zP1);

        double zHub = zP1 + PulleyLen;
        AddComp(sw, asm, md, hubP, 0, 0, zHub);

        double zYoke = zHub + HubH;
        AddComp(sw, asm, md, yokeBase, 0, 0, zYoke);

        double xArmL = -(YokeBaseL / 2.0 - ArmInset - ArmT);
        double xArmR = +(YokeBaseL / 2.0 - ArmInset - ArmT);
        Component2 cAL = AddComp(sw, asm, md, armL, xArmL, 0, zYoke);
        SetTransform(cAL, new double[] { 0, 1, 0, 0, 0, 1, -1, 0, 0 }, xArmL, 0, zYoke);
        Component2 cAR = AddComp(sw, asm, md, armR, xArmR, 0, zYoke);
        SetTransform(cAR, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xArmR, 0, zYoke);

        double zTilt = zYoke + TiltAxisZ;
        Component2 cSh = AddComp(sw, asm, md, shaftP, -TiltShaftLen / 2.0, 0, zTilt);
        SetTransform(cSh, new double[] { 0, 1, 0, 0, 0, 1, 1, 0, 0 }, -TiltShaftLen / 2.0, 0, zTilt);

        AddComp(sw, asm, md, platP, 0, 0, zTilt - PlatformT / 2.0);
        AddComp(sw, asm, md, clampP, 0.040, 0, zTilt + PlatformT / 2.0);
        AddComp(sw, asm, md, clampP, -0.040, 0, zTilt + PlatformT / 2.0);

        // Motor2 on left arm — shaft along +X (inward); part +Z maps to world +X
        double xM2 = xArmL - 0.002;
        Component2 cM2 = AddComp(sw, asm, md, motor, xM2, 0, zTilt);
        SetTransform(cM2, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xM2, 0, zTilt);

        double tip2x = xM2 + TipZ;
        double xP2 = tip2x - TipPast - PulleyLen;
        Component2 cP2 = AddComp(sw, asm, md, pulley, xP2, 0, zTilt);
        SetTransform(cP2, new double[] { 0, -1, 0, 0, 0, 1, 1, 0, 0 }, xP2, 0, zTilt);

        Log(string.Format("  M1 tipZ={0:F2} yokeZ={1:F2} tiltZ={2:F2} platform=Ø{3:F0}",
            tip1 * 1000, zYoke * 1000, zTilt * 1000, PlatformOd * 1000));
        Log(string.Format("  M2 tipX={0:F2} pulleyX={1:F2}..{2:F2}",
            tip2x * 1000, xP2 * 1000, (xP2 + PulleyLen) * 1000));

        md.EditRebuild3();
        md.ShowNamedView2("*Isometric", -1);
        md.ViewZoomtofit2();
        try { md.SaveBMP(Path.Combine(OutDir, "_preview_revopoint.bmp"), 1400, 1000); } catch { }

        string tmp = Path.Combine(OutDir, "Turntable_Revopoint2Motor_new.SLDASM");
        SaveDoc(md, tmp);
        sw.CloseDoc(md.GetTitle());
        ReplaceFile(tmp, Path.Combine(OutDir, "Turntable_Revopoint2Motor.SLDASM"));
    }

    static void CheckAliasRefs(SldWorks sw)
    {
        // Assemblies should use Motor_PM42L_048_EPAO — not the alias Motor_PM42L.SLDPRT
        Log("--- Reference check ---");
        string alias = Path.Combine(OutDir, "Motor_PM42L.SLDPRT");
        Log("Motor_PM42L.SLDPRT exists=" + File.Exists(alias) + " (alias; assemblies prefer _048_EPAO)");
        string[] asms = {
            Path.Combine(OutDir, "Motor_PM42L_048_EPAO_Pulley.SLDASM"),
            Path.Combine(OutDir, "Turntable_Revopoint2Motor.SLDASM")
        };
        foreach (string ap in asms)
        {
            int e = 0, w = 0;
            ModelDoc2 m = (ModelDoc2)sw.OpenDoc6(ap, (int)swDocumentTypes_e.swDocASSEMBLY, 1, "", ref e, ref w);
            if (m == null) { Log("OPEN FAIL " + Path.GetFileName(ap) + " e=" + e); continue; }
            AssemblyDoc a = (AssemblyDoc)m;
            object[] comps = (object[])a.GetComponents(true);
            if (comps != null)
            {
                foreach (object oc in comps)
                {
                    Component2 c = (Component2)oc;
                    string path = c.GetPathName();
                    string name = Path.GetFileName(path);
                    if (name.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Pulley", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        double[] t = (double[])c.Transform2.ArrayData;
                        Log(string.Format("  {0}: {1} tz={2:F2} mm path={3}",
                            Path.GetFileName(ap), c.Name2, t[11] * 1000, name));
                    }
                }
            }
            sw.CloseDoc(m.GetTitle());
        }
        Log("No sync of Motor_PM42L.SLDPRT — assemblies point at Motor_PM42L_048_EPAO.SLDPRT");
    }

    static void Main()
    {
        File.WriteAllText(LogPath, "=== sync user motor " + DateTime.Now.ToString("o") + " ===\r\n");
        SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        sw.Visible = true;
        // Close open docs (turntable may be dirty) so we can replace assemblies
        sw.CloseAllDocuments(true);
        System.Threading.Thread.Sleep(600);

        InspectMotor(sw);
        BuildPulleyAsm(sw);
        BuildTurntable(sw);
        CheckAliasRefs(sw);
        Log("DONE — user motor NOT overwritten");
    }
}
