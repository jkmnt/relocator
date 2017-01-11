using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;


using CamBam.UI;
using CamBam.CAD;
using CamBam.CAM;
using CamBam.Geom;
using CamBam.Values;
using CamBam;

namespace Relocator
{
    [Serializable]
    public class MOPRelocator : MOPFromGeometry, IIcon
    {
        [NonSerialized]
        private List<Point2F> _nodes = new List<Point2F>();

        //--- these are for rendering only !
        [NonSerialized]
        private List<Polyline> _visual_rapids = new List<Polyline>();

        //--- mop properties

        protected CBValue<Matrix4x4F> _transform = default(CBValue<Matrix4x4F>);

        //--- invisible and non-serializable properties

        [XmlIgnore, Browsable(false)]
        public override string MOPTypeName
        {
            get { return "Relocate"; }
        }

        [XmlIgnore, Browsable(false)]
        public Image ActiveIconImage
        {
            get { return resources.cam_relocator1; }
        }

        [XmlIgnore, Browsable(false)]
        public string ActiveIconKey
        {
            get { return "cam_relocator1"; }
        }

        [XmlIgnore, Browsable(false)]
        public Image InactiveIconImage
        {
            get { return resources.cam_relocator0; }
        }

        [XmlIgnore, Browsable(false)]
        public string InactiveIconKey
        {
            get { return "cam_relocator0"; }
        }

        //--- hidden base parameters

        [XmlIgnore, Browsable(false)]
        public new CBValue<OptimisationModes> OptimisationMode
        {
            get { return base.OptimisationMode; }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<Matrix4x4F> Transform
        {
            get { return _transform; }
            set { }
        }

        //-- read-only About field

        [
            XmlIgnore,
            Category("Misc"),
            DisplayName("Plugin Version"),
//            Description("https://github.com/jkmnt/matmill\njkmnt at git@firewood.fastmail.com")
        ]
        public string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        protected bool is_inch_units()
        {
            return base._CADFile != null && base._CADFile.DrawingUnits == Units.Inches;
        }

        protected void redraw_parameters()
        {
            CamBamUI.MainUI.ObjectProperties.Refresh();
        }

        public override List<Polyline> GetOutlines()
        {
            List<Polyline> outlines = new List<Polyline>();
            return outlines;
        }

        public override Point3F GetInitialCutPoint()
        {
            return Point3F.Undefined;
        }

        public override bool PreProcess(MachineOpToGCode gcg)
        {
            // make sure these are not changed
            base.SpindleDirection = new CBValue<SpindleDirectionOptions>(gcg._gcode.Spindle);
            base.SpindleSpeed = new CBValue<int>((int)gcg._gcode.S);
            base.ToolNumber = new CBValue<int>(gcg._gcode.Tool);

            return base.PreProcess(gcg);
        }

        public override void PostProcess(MachineOpToGCode gcg)
        {
            gcg.DefaultStockHeight = base.StockSurface.Cached;
            gcg.Clearance();

            gcg.AppendRapid(double.NaN, double.NaN, base.ClearancePlane.Cached);


            foreach (Point2F pt in _nodes)
            {
                Point3F pt3 = (Point3F)pt;
                gcg.ApplyGCodeOrigin(ref pt3);
                gcg.AppendRapid(pt3.X, pt3.Y, base.ClearancePlane.Cached);
            }

            MachineOp next_mop = get_next_mop();
            if (next_mop != null)
            {
                Point3F pt = next_mop.GetInitialCutPoint();
                Point3F pt3 = pt;

                gcg.ApplyGCodeOrigin(ref pt3);
                gcg.AppendRapid(pt3.X, pt3.Y, base.ClearancePlane.Cached);
            }
        }

        public override void Paint(ICADView iv, Display3D d3d, Color arccolor, Color linecolor, bool selected)
        {
            // XXX: what this line for ?
            base._CADFile = iv.CADFile;

            foreach(CAMPart part in base._CADFile.Parts)
            {
                if (! part.Enabled)
                    continue;

                foreach (MachineOp op in part.MachineOps)
                {
                    if (! op.Enabled)
                        continue;

                    Point3F pt = op.GetInitialCutPoint();
                    if (pt.IsUndefined)
                        continue;

                    d3d.LineWidth = 2f;
                    d3d.LineColor = Color.Orange;
                    d3d.ModelTransform = Matrix4x4F.Identity;
                    d3d.DrawIcon(pt, Display3DIcons.Circle, 10f);
                }
            }

//          foreach(Point2F pt in _nodes)
//          {
//              d3d.LineWidth = 2f;
//              d3d.LineColor = Color.Magenta;
//              d3d.ModelTransform = Matrix4x4F.Identity;
//              d3d.DrawIcon((Point3F)pt, Display3DIcons.Circle, 10f);
//          }

            d3d.LineWidth = 2f;
            d3d.LineColor = CamBamConfig.Defaults.ToolpathRapidColor;
            d3d.ModelTransform = Matrix4x4F.Identity;
            d3d.LineStyle = LineStyle.Dotted;

            Polyline p = new Polyline();

            foreach (Point2F pt in _nodes)
            {
                p.Add(pt.X, pt.Y, base.ClearancePlane.Cached);
            }

            MachineOp next_mop = get_next_mop();
            if (next_mop != null)
            {
                Point3F pt = next_mop.GetInitialCutPoint();
                if (!pt.IsUndefined)
                    p.Add(pt.X, pt.Y, base.ClearancePlane.Cached);
            }

            p.Paint(d3d);

            d3d.LineStyle = LineStyle.Solid;
        }

        private MachineOp get_next_mop()
        {
            bool found_me = false;
            foreach (CAMPart part in base._CADFile.Parts)
            {
                if (! part.Enabled) continue;

                foreach (MachineOp op in part.MachineOps)
                {
                    if (! op.Enabled) continue;
                    if (found_me) return op;
                    if (op == this) found_me = true;
                }
            }
            return null;
        }

        private MachineOp get_prev_mop()
        {
            MachineOp prev_mop = null;
            foreach (CAMPart part in base._CADFile.Parts)
            {
                if (! part.Enabled) continue;

                foreach (MachineOp op in part.MachineOps)
                {
                    if (! op.Enabled) continue;
                    if (op == this) return prev_mop;
                    prev_mop = op;
                }
            }
            return null;
        }

        private List<Point2F> import_geometry(CADFile cad, IEnumerable<int> ids)
        {
            List<Point2F> points = new List<Point2F>();

            foreach (int id in ids)
            {
                Entity entity = cad.FindPrimitive(id);

                if (entity == null)
                    continue;

                if (entity is Polyline)
                {
                    Polyline p = (Polyline)entity;
                    if (p.Closed)
                    {
                        Logger.warn("closed polylines are not supported. Ignoring entity {0}", id);
                    }
                    else
                    {
                        foreach (PolylineItem pt in p.Points)
                            points.Add((Point2F)pt.Point);
                    }
                }
                else if (entity is PointList)
                {
                    foreach (Point3F pt in ((PointList)entity).Points)
                        points.Add((Point2F)pt);
                }
                else if (entity is Line)
                {
                    Line line = (Line)entity;
                    points.Add((Point2F)line.Points[0]);
                    points.Add((Point2F)line.Points[1]);
                }
                else
                {
                    Logger.warn("ignoring usupported entity {0}", id);
                }
            }

            return points;
        }

        protected override void _GenerateToolpathsWorker()
        {
            try
            {

                _nodes.Clear();
                _visual_rapids.Clear();
                GC.Collect();

                // XXX: is it needed ?
                base.UpdateGeometryExtrema(base._CADFile);
                base._CADFile.MachiningOptions.UpdateGeometryExtrema(base._CADFile);
                _nodes = import_geometry(base._CADFile, base.PrimitiveIds);

//              if (trajectories.Count == 0)
//                  return;
//
//              base.insert_toolpaths(trajectories);

                if (base.MachineOpStatus == MachineOpStatus.Unknown)
                {
                    base.MachineOpStatus = MachineOpStatus.OK;
                }
            }
            catch (Exception ex)
            {
                base.MachineOpStatus = MachineOpStatus.Errors;
                ThisApplication.HandleException(ex);
            }
            finally
            {
                base._GenerateToolpathsFinal();
            }
        }

        public MOPRelocator(MOPRelocator src) : base(src)
        {
        }

        public MOPRelocator()
        {
        }

        public MOPRelocator(CADFile CADFile, ICollection<Entity> plist) : base(CADFile, plist)
        {
        }
    }
}