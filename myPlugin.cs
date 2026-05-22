// (C) Copyright 2026 by  
//
using System;
using Autodesk.AutoCAD.Runtime;
using Autocad_Primavera_P6_Plugin.Services;

[assembly: ExtensionApplication(typeof(Autocad_Primavera_P6_Plugin.MyPlugin))]

namespace Autocad_Primavera_P6_Plugin
{
    public class MyPlugin : IExtensionApplication
    {
        public static MyPlugin Instance { get; private set; } = null;
        public static LiteDBService MyLiteDBService { get; private set; } = null;
        public static P6ApiService MyP6ApiService { get; private set; } = null;
        public static RibbonService MyRibbonService { get; private set; } = null;

        void IExtensionApplication.Initialize()
        {
            Instance = this;  //Singleton Class pattern
            MyLiteDBService = new LiteDBService(pluginInstance: this);  //LiteDBService instance initialization
            MyP6ApiService = new P6ApiService(pluginInstance: this);  //P6ApiService instance initialization
            MyRibbonService = new RibbonService(pluginInstance: this); //RibbonService instance initialization
        }

        void IExtensionApplication.Terminate()
        {
            Instance = null;  //Singleton Class pattern
            MyLiteDBService = null;  //LiteDBService instance cleanup
            MyP6ApiService = null;  //P6ApiService instance cleanup
            MyRibbonService = null; //RibbonService instance cleanup
        }

    }

}
