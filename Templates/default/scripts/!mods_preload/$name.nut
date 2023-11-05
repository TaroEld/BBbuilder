::$Name <- {
	ID = "$name",
	Name = "RENAME",
	Version = "1.0.0"
}
::mods_registerMod(::$Name.ID, ::$Name.Version, ::$Name.Name);

::mods_queue(::$Name.ID, null, function()
{
	// ::$Name.Mod <- ::MSU.Class.Mod(::$Name.ID, ::$Name.Version, ::$Name.Name); // Delete if not needed
})