::Test <- {
	ID = "mod_test",
	Name = "Test",
	Version = "1.0.0"
}

::mods_registerMod(::Test.ID, ::Test.Version, ::Test.Name);
::mods_queue(::Test.ID, ::MSU.ID, function()
{
	::Test.Mod <- ::MSU.Class.Mod(::Test.ID, ::Test.Version, ::Test.Name);
	::mods_registerJS(::Test.ID + "/index.js");
})
