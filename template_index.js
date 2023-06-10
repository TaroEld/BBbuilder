class $Name {
    sqHandle = null
    static id = "$name";

    onConnection(sqHandle) {
        this.sqHandle = sqHandle;
        console.log("$name connected")
    }
}

registerScreen($Name.id, new $Name());