class Mod_test {
    sqHandle = null
    static id = "mod_test";

    onConnection(sqHandle) {
        this.sqHandle = sqHandle;
        console.log("mod_test connected")
    }
}

registerScreen(Mod_test.id, new Mod_test());