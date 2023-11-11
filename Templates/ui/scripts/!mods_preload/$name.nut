::$Space <- {
	ID = "$name",
	Name = "$Space",
	Version = "1.0.0"
}
::mods_registerMod(::$Space.ID, ::$Space.Version, ::$Space.Name);

::mods_queue(::$Space.ID, null, function()
{
	::$Space.Mod <- ::MSU.Class.Mod(::$Space.ID, ::$Space.Version, ::$Space.Name);
	::mods_registerJS("./mods/$Space/index.js");
	::mods_registerCSS("./mods/$Space/index.css");
})