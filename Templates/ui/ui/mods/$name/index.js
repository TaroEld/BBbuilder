class $Name {
    sqHandle = null
    static id = "$Name";

    onConnection(sqHandle) {
        this.sqHandle = sqHandle;
    }
}

registerScreen($Name.id, new $Name());
