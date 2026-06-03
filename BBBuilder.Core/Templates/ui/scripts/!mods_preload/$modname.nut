::$namespace <- {
	ID = "$modname",
	Name = "$namespace",
	Version = "1.0.0",
	// Modern Hooks Object
	MH = null,
	// MSU Object
	Mod = null,
	// JS Connection
	Connection = null,
}
// Instantiate the Modern Hooks object, add MSU as a requirement, and queue after MSU
// https://bbmodding.enduriel.com/docs/modern-hooks/mod-object/
::$namespace.MH = ::Hooks.register(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);
::$namespace.MH.require("mod_msu");
::$namespace.MH.queue(">mod_msu", function(){
	// Instantiate the MSU Object
	// https://github.com/MSUTeam/MSU/wiki/Mod
	::$namespace.Mod = ::MSU.Class.Mod(::$namespace.ID, ::$namespace.Version, ::$namespace.Name);

	// Instantiates the JS connection to the file ui/mods/$modname/$modname.js
	::$namespace.Connection = ::new("scripts/mods/msu/js_connection");
	::$namespace.Connection.m.ID = ::$namespace.Name;
	::Hooks.registerJS("ui/mods/$modname/$modname.js");
	::Hooks.registerCSS("ui/mods/$modname/$modname.css");
	::MSU.UI.registerConnection(::$namespace.Connection);

	// Includes the 'load' file of your private folder
	// Within this file, you can execute things or load more files (such as hooks)
	// as to better organise your mod, not clutter this file, and load things in order
	::include("$modname/load.nut")
});