namespace RelicLauncher.App.Services;

internal static class WikiWebViewScrollScript
{
    private const string Template =
        "(function(dx,dy){" +
        "var root=document.scrollingElement||document.documentElement||document.body;" +
        "if(root){var beforeY=root.scrollTop,beforeX=root.scrollLeft;root.scrollBy(dx,dy);" +
        "if(root.scrollTop!==beforeY||root.scrollLeft!==beforeX)return;}" +
        "function canScroll(el){if(!el)return false;var s=getComputedStyle(el);" +
        "var y=(s.overflowY==='auto'||s.overflowY==='scroll')&&el.scrollHeight>el.clientHeight+1;" +
        "var x=(s.overflowX==='auto'||s.overflowX==='scroll')&&el.scrollWidth>el.clientWidth+1;return y||x;}" +
        "var el=document.elementFromPoint(Math.floor(window.innerWidth/2),Math.floor(window.innerHeight/2));" +
        "while(el){if(canScroll(el)){el.scrollBy(dx,dy);return;}el=el.parentElement;}" +
        "window.scrollBy(dx,dy);" +
        "})({0},{1});";

    public static string Build(string dx, string dy)
        => string.Format(Template, dx, dy);
}
