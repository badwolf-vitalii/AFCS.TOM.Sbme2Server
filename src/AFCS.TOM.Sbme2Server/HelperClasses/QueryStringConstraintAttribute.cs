using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.Primitives;

namespace AFCS.TOM.Sbme2Server.HelperClasses
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class QueryStringConstraintAttribute : ActionMethodSelectorAttribute
    {
        public string ValueName { get; set; }
        public bool ValuePresent { get; set; }
        
        public QueryStringConstraintAttribute(string valueName, bool valuePresent)
        {
            ValueName = valueName;
            ValuePresent = valuePresent;
        }

        public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
        {
            var value = routeContext.HttpContext.Request.Query[ValueName];
            return ValuePresent ? !StringValues.IsNullOrEmpty(value) : StringValues.IsNullOrEmpty(value);
        }
    }
}
