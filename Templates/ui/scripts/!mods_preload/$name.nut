::$Space <- {
	ID = "$name",
	Name = "$Space",
	Version = "1.0.0",
	Connection = null,
}
::mods_registerMod(::$Space.ID, ::$Space.Version, ::$Space.Name);

::mods_queue(::$Space.ID, "mod_msu", function()
{
	::$Space.Mod <- ::MSU.Class.Mod(::$Space.ID, ::$Space.Version, ::$Space.Name);
	::$Space.Connection = ::new("scripts/mods/msu/js_connection");
	::$Space.Connection.m.ID = ::$Space.Name;
	::mods_registerJS("$name.js");
	::mods_registerCSS("$name.css");
	
	::MSU.UI.registerConnection(::$Space.Connection);
})