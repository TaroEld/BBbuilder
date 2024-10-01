::$namespace <- {
	ID = "$modname",
	Name = "$namespace",
	Version = "1.0.0",
	Connection = null,
}
::mods_registerMod(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);

::mods_queue(::$namespace.ID, "mod_msu", function()
{
	::$namespace.Mod <- ::MSU.Class.Mod(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);
	::$namespace.Connection = ::new("scripts/mods/msu/js_connection");
	::$namespace.Connection.m.ID = ::$namespace.Name;
	::mods_registerJS("$modname.js");
	::mods_registerCSS("$modname.css");
	
	::MSU.UI.registerConnection(::$namespace.Connection);
})