
using System;

using System.Collections.Generic;
using System.Xml.Serialization;
using System.Windows.Forms;
using System.IO;

using CamBam;
using CamBam.UI;
using CamBam.CAD;

// Insert logger into the relocator namespace to be bound in compile-time
namespace Relocator
{
    class Logger
    {
        static public void log(int level, string s, params object[] args)
        {
            ThisApplication.AddLogMessage(level, s, args);
        }
        static public void log(string s, params object[] args)
        {
            ThisApplication.AddLogMessage(4, s, args);
        }
        static public void warn(string s, params object[] args)
        {
            ThisApplication.AddLogMessage("Relocator warning: " + s, args);
        }
        static public void err(string s, params object[] args)
        {
            ThisApplication.AddLogMessage("Relocator error: " + s, args);
        }
    }
}

namespace Relocator
{        
    public static class Relocator_plug
    {

        private static void mop_onclick(object sender, EventArgs ars)
        {            
            MOPRelocator mop = new MOPRelocator(CamBamUI.MainUI.ActiveView.CADFile, CamBamUI.MainUI.ActiveView.Selection);
            CamBamUI.MainUI.InsertMOP(mop);
        }

        private static void insert_in_top_menu(CamBamUI ui, ToolStripMenuItem entry)
        {
            for (int i = 0; i < ui.Menus.mnuMachining.DropDownItems.Count; ++i)
            {
                ToolStripItem tsi = ui.Menus.mnuMachining.DropDownItems[i];
                if (tsi is ToolStripSeparator || i == ui.Menus.mnuMachining.DropDownItems.Count - 1)
                {
                    ui.Menus.mnuMachining.DropDownItems.Insert(i, entry);
                    return;
                }
            }
        }

        public static void InitPlugin(CamBamUI ui)
        {
            const string mop_name = "Relocate";

            ToolStripMenuItem menu_entry;

            menu_entry = new ToolStripMenuItem();
            menu_entry.Text = mop_name;
            menu_entry.Click += mop_onclick;
            menu_entry.Image = resources.cam_relocator1;

            insert_in_top_menu(ui, menu_entry);

            if (CADFile.ExtraTypes == null)
                CADFile.ExtraTypes = new List<Type>();            
            CADFile.ExtraTypes.Add(typeof(MOPRelocator));            

            {
                MOPRelocator o = new MOPRelocator();
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(MOPRelocator));
                MemoryStream stream = new MemoryStream();
                xmlSerializer.Serialize(stream, o);
            }            
        }
    }

}