## Overview
Custom version of MappingGenerator with extra features:

- External mapping classes generation
- Generate class members  

Uses comments as source for mapping information.





## Mapping rules
- Mapping Property-To-Property
  ```csharp
  target.FirstName = source.FirstName;
  target.LastName = source.LastName;
  ```
- Mapping Method Call-To-Property
  ```csharp
  target.Total = source.GetTotal()
  ```
- Flattening with sub-property
  ```csharp
  target.UnitId = source.Unit.Id
  ```
- Mapping complex property
  ```csharp
   target.MainAddress = new AddressDTO(){
  	BuildingNo = source.MainAddress.BuildingNo,
  	City = source.MainAddress.City,
  	FlatNo = source.MainAddress.FlatNo,
  	Street = source.MainAddress.Street,
  	ZipCode = source.MainAddress.ZipCode
  };
  ```
- Mapping collections
  ```csharp
  target.Addresses = source.Addresses.Select(sourceAddresse => new AddressDTO(){
    BuildingNo = sourceAddresse.BuildingNo,
    City = sourceAddresse.City,
    FlatNo = sourceAddresse.FlatNo,
    Street = sourceAddresse.Street,
    ZipCode = sourceAddresse.ZipCode
  }).ToList().AsReadOnly();
  ```
- Unwrapping wrappers 
  ```csharp
  customerEntity.Kind = cutomerDTO.Kind.Selected;
  ```
  
  ```csharp
    public enum CustomerKind
    {
        Regular,
        Premium
    }

    public class Dropdown<T>
    {
        public List<T> AllOptions { get; set; }

        public T Selected { get; set; }
    }

    public class CustomerDTO
    {
        public string Name { get; set; }
        public Dropdown<CustomerKind> Kind { get; set; }
    }

    public class UserEntity
    {
        public string Name { get; set; }
        public CustomerKind Kind { get; set; }
    }
  ```
- Using existing direct mapping constructor
  ```csharp
  target.MainAddress = new AddressDTO(source.MainAddress);
  ```

- using existing multi-parameter constuctor
  ```csharp
  this.User =  new UserDTO(firstName: entity.FirstName, lastName: entity.LastName, age: entity.Age);
  ```

