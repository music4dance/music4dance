using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace m4d.Utilities
{
    public class SongBinder : DefaultModelBinder
    {
        protected override void BindProperty(ControllerContext controllerContext, ModelBindingContext bindingContext, System.ComponentModel.PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.Name == "Length")
            {
                int? length = null;
                
                string s = controllerContext.HttpContext.Request.Form["Length"];
                if (!string.IsNullOrWhiteSpace(s)) try 
                {
                    SongDuration d = new SongDuration(s);
                    decimal l = d.Length;
                    length = (int)l;
                } 
                catch (ArgumentOutOfRangeException e)
                {
                    Trace.WriteLine(string.Format("Length Property:",e.Message));
                }
                propertyDescriptor.SetValue(bindingContext.Model, length);
            }
            else
            {
                base.BindProperty(controllerContext, bindingContext, propertyDescriptor);
            }
        }
    }
}