var $Space = function(_parent) {
    MSUBackendConnection.call(this);
    this.mModID = "$name";
    this.mNameSpace = "$Space";
}

$Space.prototype = Object.create(MSUBackendConnection.prototype);
Object.defineProperty($Space.prototype, 'constructor', {
    value: $Space,
    enumerable: false,
    writable: true
});

registerScreen("$Space", new $Space());
