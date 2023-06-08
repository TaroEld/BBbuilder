::$Name <- {
	ID = "$name",
	Name = "RENAME",
	Version = "1.0.0"
}
::mods_registerMod(::$Name.ID, ::$Name.Version, ::$Name.Name);

::mods_queue(::$Name.ID, null, function()
{
	// ::mods_registerJS(::$Name.ID + '.js'); // Delete if not needed
	// ::mods_registerCSS(::$Name.ID + '.css'); // Delete if not needed
	// ::$Name.Mod <- ::MSU.Class.Mod(::$Name.ID, ::$Name.Version, ::$Name.Name); // Delete if not needed
})