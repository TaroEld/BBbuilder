::$namespace <- {
	ID = "$modname",
	Name = "$namespace",
	Version = "1.0.0"
}
::mods_registerMod(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);

::mods_queue(::$namespace.ID, null, function()
{
	::$namespace.Mod <- ::MSU.Class.Mod(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);
	::mods_registerJS("./mods/$namespace/index.js");
	::mods_registerCSS("./mods/$namespace/index.css");
})