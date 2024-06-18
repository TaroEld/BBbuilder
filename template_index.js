class $modname {
    sqHandle = null
    static id = "$modname";

    onConnection(sqHandle) {
        this.sqHandle = sqHandle;
        console.log("$modname connected")
    }
}

registerScreen($modname.id, new $modname());