::$Space <- {
	ID = "$name",
	Name = "$Space",
	Version = "1.0.0",
	Connection = ::new("scripts/mods/msu/js_connection"),
}
::mods_registerMod(::$Space.ID, ::$Space.Version, ::$Space.Name);

::mods_queue(::$Space.ID, "mod_msu", function()
{
	::mods_registerJS("$name.js");
	::mods_registerCSS("$name.css");
	::$Space.Connection.m.ID = ::$Space.Name;
	::MSU.UI.registerConnection(::$Space.Connection);
	::$Space.Mod <- ::MSU.Class.Mod(::$Space.ID, ::$Space.Version, ::$Space.Name);
})