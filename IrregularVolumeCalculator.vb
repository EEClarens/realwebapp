' ============================================================
'  Irregular Solid Volume Calculator  –  Visual Basic .NET
'  Disk/Washer Method + Composite Simpson's Rule
'  Features: Irregular window shape + Live 3D mesh preview
'  Compile: vbc IrregularVolumeCalculator.vb /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll
' ============================================================
Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Math
Imports System.Collections.Generic

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module

' ─────────────────────────────────────────────────────────────
'  Data structure for each solid type
' ─────────────────────────────────────────────────────────────
Structure SolidDef
    Public Name    As String
    Public Desc    As String
    Public Formula As String
    Public Tags    As String()
    ' Delegate: r(x, L, R, A, N)
    Public RadiusFn As Func(Of Double, Double, Double, Double, Double, Double)
End Structure

' ─────────────────────────────────────────────────────────────
'  Main Form
' ─────────────────────────────────────────────────────────────
Public Class MainForm
    Inherits Form

    ' ── Controls ─────────────────────────────────────────────
    Private WithEvents cboSolid    As New ComboBox()
    Private WithEvents btnCalc     As New Button()
    Private WithEvents btnClose    As New Button()
    Private WithEvents btnMin      As New Button()
    Private WithEvents numL        As New NumericUpDown()
    Private WithEvents numR        As New NumericUpDown()
    Private WithEvents numA        As New NumericUpDown()
    Private WithEvents numN        As New NumericUpDown()
    Private lblDesc    As New Label()
    Private lblFormula As New Label()
    Private lblVol     As New Label()
    Private lblUnit    As New Label()
    Private lblSteps   As New Label()
    Private pnl3D      As New Panel()
    Private WithEvents tmrRotate   As New Timer()

    ' ── Solid catalogue ──────────────────────────────────────
    Private ReadOnly solids(4) As SolidDef

    ' ── 3-D state ────────────────────────────────────────────
    Private rotY        As Double = 0.3
    Private rotX        As Double = 0.18
    Private drag3D      As Boolean = False
    Private drag3DPt    As Point
    Private dragWin     As Boolean = False
    Private dragWinPt   As Point
    Private lastVolume  As Double = -1

    ' ── Colours / fonts ──────────────────────────────────────
    Private ReadOnly clrBg     As Color = Color.FromArgb(8, 12, 38)
    Private ReadOnly clrPanel  As Color = Color.FromArgb(14, 20, 55)
    Private ReadOnly clrAccent As Color = Color.FromArgb(64, 148, 255)
    Private ReadOnly clrGold   As Color = Color.FromArgb(255, 210, 80)
    Private ReadOnly clrText   As Color = Color.FromArgb(210, 220, 255)
    Private ReadOnly fntTitle  As New Font("Segoe UI", 13, FontStyle.Bold)
    Private ReadOnly fntBody   As New Font("Segoe UI", 9)
    Private ReadOnly fntMono   As New Font("Courier New", 8.5F)
    Private ReadOnly fntBig    As New Font("Segoe UI", 22, FontStyle.Bold)
    Private ReadOnly fntLabel  As New Font("Segoe UI", 8, FontStyle.Bold)

    ' ─────────────────────────────────────────────────────────
    Public Sub New()
        Me.Text            = "Irregular Solid Volume Calculator"
        Me.Size            = New Size(680, 820)
        Me.StartPosition   = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.None
        Me.BackColor       = clrBg
        Me.DoubleBuffered  = True

        InitSolids()
        BuildUI()
        ApplyIrregularShape()

        tmrRotate.Interval = 40
        tmrRotate.Start()

        cboSolid.SelectedIndex = 0
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Solid definitions
    ' ─────────────────────────────────────────────────────────
    Private Sub InitSolids()
        solids(0) = New SolidDef With {
            .Name    = "1 · Sinusoidal Paraboloid Shell",
            .Desc    = "A parabolic solid modulated by a sine wave, producing a ribbed shell-like surface with periodic undulations along its length.",
            .Formula = "r(x) = R·(1−(x/L)²) + A·sin(N·π·x/L)",
            .Tags    = {"Ribbed", "Shell", "Organic"},
            .RadiusFn = Function(x, L, R, A, N) R*(1-(x/L)^2) + A*Sin(N*PI*x/L)
        }
        solids(1) = New SolidDef With {
            .Name    = "2 · Damped-Wave Frustum",
            .Desc    = "A cone-like frustum whose radius decays with an oscillating cosine envelope — mimicking a vibrating body settling to rest.",
            .Formula = "r(x) = R + (A−R)·(x/L) + N·cos(3x)·e^(−0.3x)",
            .Tags    = {"Acoustic", "Frustum", "Decaying"},
            .RadiusFn = Function(x, L, R, A, N) Math.Max(0, R+(A-R)*(x/L)+N*Cos(3*x)*Exp(-0.3*x))
        }
        solids(2) = New SolidDef With {
            .Name    = "3 · Exponential Ogive Dome",
            .Desc    = "A dome whose profile follows an exponential ogive curve — wide base tapering smoothly to a rounded apex, common in ballistics.",
            .Formula = "r(x) = R·√(1−(x/L)²)·e^(−A·x/L)",
            .Tags    = {"Dome", "Ogive", "Ballistic"},
            .RadiusFn = Function(x, L, R, A, N) R*Sqrt(Math.Max(0,1-(x/L)^2))*Exp(-A*x/L)
        }
        solids(3) = New SolidDef With {
            .Name    = "4 · Hyperbolic Annular Solid",
            .Desc    = "A solid bounded by a hyperbolic curve — narrower at the waist and flared symmetrically at both ends.",
            .Formula = "r(x) = √(R² + (x−L/2)²/A²)",
            .Tags    = {"Hyperboloid", "Ring", "Flared"},
            .RadiusFn = Function(x, L, R, A, N) Sqrt(R*R+(x-L/2)^2/(A*A))
        }
        solids(4) = New SolidDef With {
            .Name    = "5 · Polynomial Biconcave Spindle",
            .Desc    = "A lens-shaped solid whose radius follows a degree-4 polynomial — concave at both ends and convex at the centre, like a red blood cell.",
            .Formula = "r(x) = R·[4·(x/L)·(1−x/L)]^N",
            .Tags    = {"Biconcave", "Lens", "Cell-like"},
            .RadiusFn = Function(x, L, R, A, N)
                            Dim b As Double = 4*(x/L)*(1-x/L)
                            Return If(b > 0, R*b^N, 0.0)
                        End Function
        }
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Build all UI controls
    ' ─────────────────────────────────────────────────────────
    Private Sub BuildUI()
        Dim W As Integer = Me.ClientSize.Width
        Dim pad As Integer = 20

        ' ── Title bar (draggable) ─────────────────────────────
        Dim pnlTitle As New Panel() With {
            .Bounds    = New Rectangle(0, 0, W, 52),
            .BackColor = Color.FromArgb(12, 18, 55)
        }
        AddHandler pnlTitle.Paint,     AddressOf PnlTitle_Paint
        AddHandler pnlTitle.MouseDown, AddressOf WinDrag_Down
        AddHandler pnlTitle.MouseMove, AddressOf WinDrag_Move
        AddHandler pnlTitle.MouseUp,   AddressOf WinDrag_Up

        btnClose.Bounds    = New Rectangle(W-40, 10, 28, 28)
        btnClose.Text      = "✕"
        Style_ChromeBtn(btnClose, Color.FromArgb(200, 60, 60))
        pnlTitle.Controls.Add(btnClose)

        btnMin.Bounds  = New Rectangle(W-76, 10, 28, 28)
        btnMin.Text    = "─"
        Style_ChromeBtn(btnMin, Color.FromArgb(60, 60, 90))
        pnlTitle.Controls.Add(btnMin)

        Me.Controls.Add(pnlTitle)

        ' ── Selector ─────────────────────────────────────────
        Dim y As Integer = 66

        MakeLabel(Me, "SELECT SOLID FORM", pad, y, 200)
        y += 20

        cboSolid.Bounds        = New Rectangle(pad, y, W - pad*2, 30)
        cboSolid.DropDownStyle = ComboBoxStyle.DropDownList
        cboSolid.Font          = fntBody
        cboSolid.BackColor     = Color.FromArgb(20, 28, 70)
        cboSolid.ForeColor     = clrText
        cboSolid.FlatStyle     = FlatStyle.Flat
        For Each s In solids : cboSolid.Items.Add(s.Name) : Next
        Me.Controls.Add(cboSolid)
        y += 38

        ' ── Description + formula ────────────────────────────
        lblDesc.Bounds    = New Rectangle(pad, y, W-pad*2, 48)
        lblDesc.Font      = fntBody
        lblDesc.ForeColor = clrText
        lblDesc.BackColor = Color.FromArgb(18, 26, 62)
        lblDesc.Padding   = New Padding(8, 6, 8, 0)
        Me.Controls.Add(lblDesc)
        y += 52

        lblFormula.Bounds    = New Rectangle(pad, y, W-pad*2, 26)
        lblFormula.Font      = fntMono
        lblFormula.ForeColor = clrGold
        lblFormula.BackColor = Color.FromArgb(28, 22, 8)
        lblFormula.Padding   = New Padding(8, 5, 0, 0)
        Me.Controls.Add(lblFormula)
        y += 34

        ' ── Parameter grid ───────────────────────────────────
        Dim half As Integer = (W - pad*2 - 10) \ 2
        AddNud(Me, "Length  L (cm)",        numL, pad,          y, half, 0.1D, 1000D, 10D)
        AddNud(Me, "Max Radius  R (cm)",    numR, pad+half+10,  y, half, 0.1D, 500D,  5D)
        y += 54
        AddNud(Me, "Amplitude / Factor  A", numA, pad,          y, half, 0.01D, 100D, 1D)
        AddNud(Me, "Shape Param  n / B",    numN, pad+half+10,  y, half, 0.1D, 20D,  3D)
        y += 54

        ' ── Calculate button ─────────────────────────────────
        btnCalc.Bounds    = New Rectangle(pad, y, W-pad*2, 44)
        btnCalc.Text      = "▶   C A L C U L A T E   V O L U M E"
        btnCalc.Font      = New Font("Segoe UI", 11, FontStyle.Bold)
        btnCalc.BackColor = clrAccent
        btnCalc.ForeColor = Color.White
        btnCalc.FlatStyle = FlatStyle.Flat
        btnCalc.FlatAppearance.BorderSize = 0
        btnCalc.Cursor    = Cursors.Hand
        Me.Controls.Add(btnCalc)
        y += 52

        ' ── Result display ───────────────────────────────────
        Dim pnlResult As New Panel() With {
            .Bounds    = New Rectangle(pad, y, W-pad*2, 76),
            .BackColor = Color.FromArgb(14, 20, 55)
        }
        AddHandler pnlResult.Paint, AddressOf PnlResult_Paint

        lblVol.Bounds    = New Rectangle(12, 8, 380, 42)
        lblVol.Font      = fntBig
        lblVol.ForeColor = clrGold
        lblVol.BackColor = Color.Transparent
        lblVol.Text      = "—"
        pnlResult.Controls.Add(lblVol)

        lblUnit.Bounds    = New Rectangle(12, 50, 300, 18)
        lblUnit.Font      = fntBody
        lblUnit.ForeColor = Color.FromArgb(140, 150, 200)
        lblUnit.BackColor = Color.Transparent
        lblUnit.Text      = "cubic centimetres  (cm³)"
        pnlResult.Controls.Add(lblUnit)

        Me.Controls.Add(pnlResult)
        y += 82

        ' ── Calculation steps ────────────────────────────────
        lblSteps.Bounds    = New Rectangle(pad, y, W-pad*2, 62)
        lblSteps.Font      = fntMono
        lblSteps.ForeColor = Color.FromArgb(130, 160, 220)
        lblSteps.BackColor = Color.FromArgb(10, 14, 40)
        lblSteps.Padding   = New Padding(8, 6, 0, 0)
        Me.Controls.Add(lblSteps)
        y += 68

        ' ── 3-D canvas ───────────────────────────────────────
        pnl3D.Bounds    = New Rectangle(pad, y, W-pad*2, Me.ClientSize.Height - y - pad - 20)
        pnl3D.BackColor = Color.FromArgb(6, 8, 26)
        AddHandler pnl3D.Paint,     AddressOf Render3D
        AddHandler pnl3D.MouseDown, AddressOf Drag3D_Down
        AddHandler pnl3D.MouseMove, AddressOf Drag3D_Move
        AddHandler pnl3D.MouseUp,   AddressOf Drag3D_Up
        Me.Controls.Add(pnl3D)
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Irregular window shape  (clipped octagonal / asymmetric)
    ' ─────────────────────────────────────────────────────────
    Private Sub ApplyIrregularShape()
        Dim W As Integer = Me.Width
        Dim H As Integer = Me.Height
        Dim gp As New GraphicsPath()
        ' Asymmetric clipped polygon — clearly non-rectangular
        Dim pts() As Point = {
            New Point(38, 0),         ' top-left bevel
            New Point(W - 38, 0),     ' top-right bevel
            New Point(W, 38),
            New Point(W - 12, H \ 3), ' right dent
            New Point(W, H \ 3 + 30),
            New Point(W, H - 55),
            New Point(W - 70, H),     ' bottom-right bevel
            New Point(70, H),         ' bottom-left bevel
            New Point(0, H - 55),
            New Point(12, H \ 3 + 30), ' left dent
            New Point(0, H \ 3),
            New Point(0, 38)
        }
        gp.AddPolygon(pts)
        Me.Region = New Region(gp)
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Simpson's Rule integration
    ' ─────────────────────────────────────────────────────────
    Private Function Simpson(sd As SolidDef,
                             L As Double, R As Double,
                             A As Double, N As Double,
                             Optional nSlices As Integer = 1000) As Double
        If nSlices Mod 2 <> 0 Then nSlices += 1
        Dim h  As Double = L / nSlices
        Dim sm As Double = 0
        For i As Integer = 0 To nSlices
            Dim x  As Double = i * h
            Dim rv As Double = sd.RadiusFn(x, L, R, A, N)
            Dim fx As Double = PI * rv * rv
            Dim c  As Integer = If(i = 0 OrElse i = nSlices, 1,
                                   If(i Mod 2 = 1, 4, 2))
            sm += c * fx
        Next
        Return sm * h / 3.0
    End Function

    ' ─────────────────────────────────────────────────────────
    '  Sample radii for 3-D mesh
    ' ─────────────────────────────────────────────────────────
    Private Function SampleRadii(sd As SolidDef, L As Double, R As Double,
                                 A As Double, N As Double,
                                 slices As Integer) As Double()
        Dim arr(slices) As Double
        For i As Integer = 0 To slices
            Dim x As Double = i * L / slices
            arr(i) = Math.Max(0, sd.RadiusFn(x, L, R, A, N))
        Next
        Return arr
    End Function

    ' ─────────────────────────────────────────────────────────
    '  3-D render  (solid of revolution, lit mesh)
    ' ─────────────────────────────────────────────────────────
    Private Sub Render3D(sender As Object, e As PaintEventArgs)
        Dim g  As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim PW As Integer = pnl3D.Width
        Dim PH As Integer = pnl3D.Height
        Dim cx As Single = PW / 2.0F
        Dim cy As Single = PH / 2.0F

        ' Gradient background
        Using bgBr As New LinearGradientBrush(
                New Rectangle(0, 0, PW, PH),
                Color.FromArgb(6, 8, 26), Color.FromArgb(14, 18, 50),
                LinearGradientMode.ForwardDiagonal)
            g.FillRectangle(bgBr, 0, 0, PW, PH)
        End Using

        Dim idx As Integer = cboSolid.SelectedIndex
        If idx < 0 Then Return
        Dim L As Double = CDbl(numL.Value)
        Dim R As Double = CDbl(numR.Value)
        Dim A As Double = CDbl(numA.Value)
        Dim N As Double = CDbl(numN.Value)

        ' Clamp rotX
        rotX = Math.Max(-PI/2 + 0.1, Math.Min(PI/2 - 0.1, rotX))

        Const SLICES As Integer = 60   ' along axis
        Const RINGS  As Integer = 48   ' around axis

        Dim radii() As Double = SampleRadii(solids(idx), L, R, A, N, SLICES)
        Dim maxR As Double = 0
        For Each rv In radii : If rv > maxR Then maxR = rv : Next
        If maxR < 0.001 Then maxR = 1

        ' Scale so solid fits panel
        Dim scaleAxis As Single = CSng((PW * 0.38) / L)
        Dim scaleRad  As Single = CSng((PH * 0.35) / maxR)
        Dim sc As Single = Math.Min(scaleAxis, scaleRad)

        ' Pre-compute rotation trig
        Dim cosY As Double = Cos(rotY)
        Dim sinY As Double = Sin(rotY)
        Dim cosX As Double = Cos(rotX)
        Dim sinX As Double = Sin(rotX)

        ' Build projected screen points  pts(i, j)
        Dim pts(SLICES, RINGS) As PointF
        Dim zBuf(SLICES, RINGS) As Double

        For i As Integer = 0 To SLICES
            For j As Integer = 0 To RINGS
                Dim angle As Double = j * 2 * PI / RINGS
                Dim ax As Double = (i * L / SLICES - L / 2.0) * sc
                Dim ay As Double = radii(i) * sc * Sin(angle)
                Dim az As Double = radii(i) * sc * Cos(angle)

                ' Rotate Y then X
                Dim rx As Double = ax * cosY - az * sinY
                Dim rz As Double = ax * sinY + az * cosY
                Dim ry As Double = ay * cosX - rz * sinX
                Dim rz2 As Double = ay * sinX + rz * cosX

                ' Perspective divide
                Dim fov  As Double = 600
                Dim persp As Double = fov / (fov + rz2 + 200)
                pts(i, j)  = New PointF(CSng(cx + rx * persp), CSng(cy - ry * persp))
                zBuf(i, j) = rz2
            Next
        Next

        ' Collect quads with avg depth for painter's sort
        Dim quads As New List(Of (depth As Double, pts As PointF(), bright As Single))
        For i As Integer = 0 To SLICES - 1
            For j As Integer = 0 To RINGS - 1
                Dim j1 As Integer = (j + 1) Mod RINGS
                Dim p0 = pts(i,   j) : Dim p1 = pts(i+1, j)
                Dim p2 = pts(i+1, j1): Dim p3 = pts(i,   j1)
                Dim depth As Double = (zBuf(i,j)+zBuf(i+1,j)+zBuf(i+1,j1)+zBuf(i,j1)) / 4.0

                ' Simple diffuse lighting: normal via cross-product approximation
                Dim dx As Single = p1.X - p0.X : Dim dy As Single = p1.Y - p0.Y
                Dim ex As Single = p3.X - p0.X : Dim ey As Single = p3.Y - p0.Y
                Dim nz As Single = dx * ey - dy * ex  ' z-component of normal
                Dim bright As Single = CSng(Math.Abs(nz) / (Math.Abs(nz) + 200.0F) * 0.7F + 0.3F)

                quads.Add((depth, {p0, p1, p2, p3}, bright))
            Next
        Next

        ' Sort back-to-front
        quads.Sort(Function(a, b) b.depth.CompareTo(a.depth))

        ' Draw quads with colour depending on solid index
        For Each q In quads
            Dim hue As Double = (idx / 4.0) * 200 + 160  ' degrees
            Dim clr As Color = HsvToColor(hue, 0.72, q.bright)
            Using br As New SolidBrush(Color.FromArgb(210, clr))
                g.FillPolygon(br, q.pts)
            End Using
            Using pen As New Pen(Color.FromArgb(30, 255, 255, 255), 0.3F)
                g.DrawPolygon(pen, q.pts)
            End Using
        Next

        ' Axis line
        Dim axL As New PointF(cx - CSng(L/2*sc*cosY*0.9), cy)
        Dim axR As New PointF(cx + CSng(L/2*sc*cosY*0.9), cy)
        Using pen As New Pen(Color.FromArgb(60, 120, 180, 255), 1)
            pen.DashStyle = DashStyle.Dash
            g.DrawLine(pen, axL, axR)
        End Using

        ' HUD text
        Dim hudFont As New Font("Segoe UI", 8)
        g.DrawString("Drag to rotate  ·  3D Solid of Revolution",
                     hudFont, New SolidBrush(Color.FromArgb(100, 160, 220)),
                     8, PH - 20)
        If lastVolume > 0 Then
            Dim volTxt As String = $"V = {lastVolume:F2} cm³"
            Dim sz As SizeF = g.MeasureString(volTxt, hudFont)
            g.DrawString(volTxt, hudFont,
                         New SolidBrush(clrGold),
                         PW - sz.Width - 8, PH - 20)
        End If
        hudFont.Dispose()
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  HSV → Color helper
    ' ─────────────────────────────────────────────────────────
    Private Function HsvToColor(h As Double, s As Double, v As Double) As Color
        h = ((h Mod 360) + 360) Mod 360
        Dim hi As Integer = CInt(Math.Floor(h / 60)) Mod 6
        Dim f  As Double  = h / 60 - Math.Floor(h / 60)
        Dim pv As Integer = CInt(v * (1 - s) * 255)
        Dim qv As Integer = CInt(v * (1 - f * s) * 255)
        Dim tv As Integer = CInt(v * (1 - (1 - f) * s) * 255)
        Dim iv As Integer = CInt(v * 255)
        Select Case hi
            Case 0 : Return Color.FromArgb(iv, tv, pv)
            Case 1 : Return Color.FromArgb(qv, iv, pv)
            Case 2 : Return Color.FromArgb(pv, iv, tv)
            Case 3 : Return Color.FromArgb(pv, qv, iv)
            Case 4 : Return Color.FromArgb(tv, pv, iv)
            Case Else : Return Color.FromArgb(iv, pv, qv)
        End Select
    End Function

    ' ─────────────────────────────────────────────────────────
    '  Paint form border (irregular outline)
    ' ─────────────────────────────────────────────────────────
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g  As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim W As Integer = Me.ClientSize.Width
        Dim H As Integer = Me.ClientSize.Height
        Dim gp As New GraphicsPath()
        Dim pts() As Point = {
            New Point(38, 0), New Point(W-38, 0), New Point(W, 38),
            New Point(W-12, H\3), New Point(W, H\3+30),
            New Point(W, H-55), New Point(W-70, H), New Point(70, H),
            New Point(0, H-55), New Point(12, H\3+30), New Point(0, H\3),
            New Point(0, 38)
        }
        gp.AddPolygon(pts)
        Using pen As New Pen(clrAccent, 1.5F)
            g.DrawPath(pen, gp)
        End Using
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Title bar paint (gradient + subtitle)
    ' ─────────────────────────────────────────────────────────
    Private Sub PnlTitle_Paint(sender As Object, e As PaintEventArgs)
        Dim p  As Panel = CType(sender, Panel)
        Dim g  As Graphics = e.Graphics
        Using br As New LinearGradientBrush(p.ClientRectangle,
                Color.FromArgb(20, 30, 80), Color.FromArgb(10, 16, 52),
                LinearGradientMode.Horizontal)
            g.FillRectangle(br, p.ClientRectangle)
        End Using
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit
        g.DrawString("📐  Irregular Solid Volume Calculator", fntTitle,
                     New SolidBrush(clrText), 16, 8)
        g.DrawString("Disk / Washer Method  +  Composite Simpson's Rule",
                     fntBody, New SolidBrush(Color.FromArgb(120, 150, 210)), 18, 32)
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Result panel paint (subtle border)
    ' ─────────────────────────────────────────────────────────
    Private Sub PnlResult_Paint(sender As Object, e As PaintEventArgs)
        Dim p As Panel = CType(sender, Panel)
        Dim g As Graphics = e.Graphics
        g.FillRectangle(New SolidBrush(Color.FromArgb(14, 20, 55)), p.ClientRectangle)
        Using pen As New Pen(clrAccent, 1)
            g.DrawRectangle(pen, 0, 0, p.Width-1, p.Height-1)
        End Using
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  Event handlers
    ' ─────────────────────────────────────────────────────────
    Private Sub cboSolid_SelectedIndexChanged(sender As Object, e As EventArgs) _
            Handles cboSolid.SelectedIndexChanged
        Dim idx As Integer = cboSolid.SelectedIndex
        If idx < 0 Then Return
        lblDesc.Text    = solids(idx).Desc
        lblFormula.Text = "  " & solids(idx).Formula
        lblVol.Text     = "—"
        lblSteps.Text   = ""
        lastVolume      = -1
        pnl3D.Invalidate()
    End Sub

    Private Sub btnCalc_Click(sender As Object, e As EventArgs) Handles btnCalc.Click
        Dim idx As Integer = cboSolid.SelectedIndex
        Dim L   As Double  = CDbl(numL.Value)
        Dim R   As Double  = CDbl(numR.Value)
        Dim A   As Double  = CDbl(numA.Value)
        Dim N   As Double  = CDbl(numN.Value)

        If L <= 0 OrElse R <= 0 Then
            MessageBox.Show("Length and Radius must be greater than 0.", "Input Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim V As Double = Simpson(solids(idx), L, R, A, N)
        lastVolume      = V
        lblVol.Text     = $"{V:F2} cm³"
        Dim dX As Double = L / 1000
        lblSteps.Text   = $"Method : Disk/Washer  →  V = π∫[r(x)]² dx{vbCrLf}" &
                          $"Rule   : Composite Simpson's  |  n = 1 000 slices{vbCrLf}" &
                          $"Δx = {dX:F5} cm   |   x = 0 → {L} cm   |   V = {V:F4} cm³"
        pnl3D.Invalidate()
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub btnMin_Click(sender As Object, e As EventArgs) Handles btnMin.Click
        Me.WindowState = FormWindowState.Minimized
    End Sub

    Private Sub tmrRotate_Tick(sender As Object, e As EventArgs) Handles tmrRotate.Tick
        If Not drag3D Then
            rotY += 0.015
            pnl3D.Invalidate()
        End If
    End Sub

    ' ── 3-D drag ─────────────────────────────────────────────
    Private Sub Drag3D_Down(sender As Object, e As MouseEventArgs)
        drag3D   = True
        drag3DPt = e.Location
        tmrRotate.Stop()
    End Sub

    Private Sub Drag3D_Move(sender As Object, e As MouseEventArgs)
        If Not drag3D Then Return
        rotY += (e.X - drag3DPt.X) * 0.012
        rotX += (e.Y - drag3DPt.Y) * 0.012
        drag3DPt = e.Location
        pnl3D.Invalidate()
    End Sub

    Private Sub Drag3D_Up(sender As Object, e As MouseEventArgs)
        drag3D = False
        tmrRotate.Start()
    End Sub

    ' ── Window drag ──────────────────────────────────────────
    Private Sub WinDrag_Down(sender As Object, e As MouseEventArgs)
        dragWin   = True
        dragWinPt = e.Location
    End Sub

    Private Sub WinDrag_Move(sender As Object, e As MouseEventArgs)
        If Not dragWin Then Return
        Me.Location = New Point(Me.Left + e.X - dragWinPt.X,
                                Me.Top  + e.Y - dragWinPt.Y)
    End Sub

    Private Sub WinDrag_Up(sender As Object, e As MouseEventArgs)
        dragWin = False
    End Sub

    ' ─────────────────────────────────────────────────────────
    '  UI helper methods
    ' ─────────────────────────────────────────────────────────
    Private Sub Style_ChromeBtn(btn As Button, bg As Color)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 0
        btn.BackColor = bg
        btn.ForeColor = Color.White
        btn.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        btn.Cursor    = Cursors.Hand
    End Sub

    Private Sub MakeLabel(parent As Control, txt As String, x As Integer, y As Integer, w As Integer)
        Dim lbl As New Label() With {
            .Text      = txt,
            .Bounds    = New Rectangle(x, y, w, 18),
            .Font      = fntLabel,
            .ForeColor = Color.FromArgb(90, 120, 200),
            .BackColor = Color.Transparent
        }
        parent.Controls.Add(lbl)
    End Sub

    Private Sub AddNud(parent As Control, caption As String, nud As NumericUpDown,
                       x As Integer, y As Integer, w As Integer,
                       mn As Decimal, mx As Decimal, def As Decimal)
        MakeLabel(parent, caption, x, y, w)
        nud.Bounds        = New Rectangle(x, y + 20, w, 28)
        nud.Minimum       = mn
        nud.Maximum       = mx
        nud.Value         = def
        nud.DecimalPlaces = 2
        nud.Increment     = 0.1D
        nud.Font          = fntBody
        nud.BackColor     = Color.FromArgb(20, 28, 70)
        nud.ForeColor     = clrText
        parent.Controls.Add(nud)
    End Sub

End Class
