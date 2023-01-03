::RENAME <- {
	ID = "$name",
	Name = "RENAME",
	Version = "1.0.0"
}
::mods_registerMod(::RENAME.ID, ::RENAME.Version, ::RENAME.Name);

::mods_queue(::RENAME.ID, null, function()
{
	// ::mods_registerJS(::RENAME.ID + '.js'); // Delete if not needed
	// ::mods_registerCSS(::RENAME.ID + '.css'); // Delete if not needed
	// ::RENAME.Mod <- ::MSU.Class.Mod(::RENAME.ID, ::RENAME.Version, ::RENAME.Name); // Delete if not needed

})
