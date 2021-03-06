using System;
using System.Drawing;
using System.Drawing.Design;
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
        [NonSerialized]
        private Point3F _start = Point3F.Undefined;

        //--- mop properties
        protected double _move_feedrate = 0;

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
        public new CBValue<double> CutFeedrate
        {
            get { return new CBValue<double>(); }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<double> PlungeFeedrate
        {
            get { return new CBValue<double>(); }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<Point3F> StartPoint
        {
            get { return new CBValue<Point3F>(); }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<RoughingFinishingOptions> RoughingFinishing
        {
            get { return new CBValue<RoughingFinishingOptions>(); }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<double> RoughingClearance
        {
            get { return new CBValue<double>(0.0); }
            set { }
        }

        [XmlIgnore, Browsable(false)]
        public new CBValue<Matrix4x4F> Transform
        {
        	get { return new CBValue<Matrix4x4F>(Matrix4x4F.Identity);}
        	set { }
        }

        //-- mop-specific properties
        [
            CBKeyValue,
            Category("Feedrates"),
            DefaultValue(typeof(CBValue<double>), "Default"),
            Description("The feed rate to use when moving. If 0 use rapids (G0)"),
            DisplayName("Move Feedrate")
        ]
        public double Move_feedrate
        {
            get { return this._move_feedrate; }
            set { this._move_feedrate = value; }
        }

        //-- read-only About field

        [
            XmlIgnore,
            Category("Misc"),
            DisplayName("Plugin Version"),
            Description("https://github.com/jkmnt/relocator\njkmnt at git@firewood.fastmail.com")
        ]
        public string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        public override Point3F GetInitialCutPoint()
        {
            return Point3F.Undefined;
        }

        public override void PostProcess(MachineOpToGCode gcg)
        {
            Point3F offset = new Point3F(0,0,0);
            gcg.ApplyGCodeOrigin(ref offset);
            _start = new Point3F(gcg._gcode.X, gcg._gcode.Y, gcg._gcode.Z) - offset;
            double feedrate = _move_feedrate == 0 ? double.NaN : _move_feedrate;

            double level = Math.Max(base.ClearancePlane.Cached, gcg._gcode.Z);

            gcg.AppendRapid(gcg._gcode.X, gcg._gcode.Y, level);

            foreach (Point2F pt in _nodes)
            {
                Point3F pt3 = (Point3F)pt;
                gcg.ApplyGCodeOrigin(ref pt3);
                gcg.AppendMove(double.IsNaN(feedrate) ? "g0" : "g1", pt3.X, pt3.Y, level, feedrate);
            }

            MachineOp next_mop = get_next_mop(get_active_mops());
            if (next_mop != null)
            {
                Point3F pt = next_mop.GetInitialCutPoint();
                Point3F pt3 = pt;

                gcg.ApplyGCodeOrigin(ref pt3);
                gcg.AppendMove(double.IsNaN(feedrate) ? "g0" : "g1", pt3.X, pt3.Y, level, feedrate);
            }
        }

        public override void Paint(ICADView iv, Display3D d3d, Color arccolor, Color linecolor, bool selected)
        {
            // XXX: what this line for ?
            base._CADFile = iv.CADFile;

            List<MachineOp> all_mops = get_active_mops();
            MachineOp next_mop = get_next_mop(all_mops);

            double level = base.ClearancePlane.Cached;

            if (! _start.IsUndefined)
            {
                level = Math.Max(level, _start.Z);
            }

            // paint all startpoints. starpoint of the next op (out end) is painted selected if we're selected
            d3d.LineColor = Color.Orange;
            d3d.ModelTransform = Matrix4x4F.Identity;

            foreach (MachineOp mop in all_mops)
            {
                if (mop == this) continue;

                Point3F pt = mop.GetInitialCutPoint();
                if (pt.IsUndefined)
                    continue;

                d3d.LineWidth = (mop == next_mop && selected) ? 2F : 1F;
                d3d.DrawIcon(pt, Display3DIcons.Circle, 10f);
            }

            // collect rapids and paint 'em
            Polyline p = new Polyline();

            if (! _start.IsUndefined)
            {
                p.Add(_start);
                p.Add(_start.X, _start.Y, level);
            }

            foreach (Point2F pt in _nodes)
            {
                p.Add(pt.X, pt.Y, level);
            }

            if (next_mop != null)
            {
                Point3F pt = next_mop.GetInitialCutPoint();
                if (!pt.IsUndefined)
                {
                    p.Add(pt.X, pt.Y, level);
                    p.Add(pt);
                }
            }

            d3d.LineWidth = selected ? 2f : 1f;
            d3d.LineColor = CamBamConfig.Defaults.ToolpathRapidColor;
            d3d.LineStyle = LineStyle.Dotted;

            p.Paint(d3d);

            d3d.LineStyle = LineStyle.Solid;
        }

        private List<MachineOp> get_active_mops()
        {
            List<MachineOp> mops = new List<MachineOp>();

            foreach (CAMPart part in base._CADFile.Parts)
            {
                if (! part.Enabled) continue;
                foreach (MachineOp mop in part.MachineOps)
                {
                    if (! mop.Enabled) continue;
                    mops.Add(mop);
                }
            }
            return mops;
        }

        private MachineOp get_next_mop(List<MachineOp> mops)
        {
            int idx = mops.IndexOf(this) + 1;
            return idx < mops.Count ? mops[idx] : null;
        }

        private MachineOp get_prev_mop(List<MachineOp> mops)
        {
            int idx = mops.IndexOf(this) - 1;
            return idx >= 0 ? mops[idx] : null;
        }

        private List<Point2F> import_geometry(CADFile cad, IEnumerable<int> ids)
        {
            List<Point2F> points = new List<Point2F>();

            if (ids == null)
                return points;

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
                _start = Point3F.Undefined;
                _nodes = import_geometry(base._CADFile, base.PrimitiveIds);

                if (base.MachineOpStatus == MachineOpStatus.Unknown)
                    base.MachineOpStatus = MachineOpStatus.OK;
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

        public override MachineOp Clone()
        {
        	return new MOPRelocator(this);
        }


        public MOPRelocator()
        {
        }

        public MOPRelocator(MOPFromGeometry src) : base(src)
        {
        }

        public MOPRelocator(MOPRelocator src) : base(src)
        {
            this.Move_feedrate = src.Move_feedrate;
        }

        public MOPRelocator(CADFile cad, ICollection<Entity> plist) : base(cad, plist)
        {
        }
    }
}