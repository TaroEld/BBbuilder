var $name = {
    sqHandle : null,
    ModID : "$Name",

    onConnection : function(sqHandle) {
        this.sqHandle = sqHandle;
    }
}

registerScreen($Name.id, $Name);
