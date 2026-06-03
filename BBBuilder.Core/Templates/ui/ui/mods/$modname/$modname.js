var $namespace = function(_parent) {
    MSUBackendConnection.call(this);
    this.mModID = "$modname";
    this.mNameSpace = "$namespace";
}

$namespace.prototype = Object.create(MSUBackendConnection.prototype);
Object.defineProperty($namespace.prototype, 'constructor', {
    value: $namespace,
    enumerable: false,
    writable: true
});

registerScreen("$namespace", new $namespace());
