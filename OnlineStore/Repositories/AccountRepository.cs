﻿using OnlineStore.Data;
using System.Threading.Tasks;
using System.Linq;
using OnlineStore.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OnlineStore.Models.Account;

namespace OnlineStore.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext dbContext;
        private readonly Sha256Helper sha256Helper;
        private readonly IHttpContextAccessor httpContextAccessor;

        public AccountRepository(AppDbContext dbContext,
                                 Sha256Helper sha256Helper,
                                 IHttpContextAccessor httpContextAccessor)
        {
            this.dbContext = dbContext;
            this.sha256Helper = sha256Helper;
            this.httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> SignInAsync(string email, string password)
        {
            var hashedPassword = sha256Helper.Hash(password);
            var account = await dbContext.Users
                .Where(u => u.Email.Equals(email) && u.Password.Equals(hashedPassword))
                .FirstOrDefaultAsync();

            if (account == null)
                return false;

            var accountClaims = new List<Claim>()
            {
                new Claim(ClaimTypes.Sid, account.Id.ToString()),
                new Claim(ClaimTypes.Email, account.Email),
                new Claim(ClaimTypes.Role, account.Role.ToString())
            };
            var accountClaimsIdentity = new ClaimsIdentity(accountClaims, 
                CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(accountClaimsIdentity);

            await httpContextAccessor.HttpContext
                .SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return true;
        }

        public Task SignOutAsync() =>
            httpContextAccessor.HttpContext
            .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        public Task<bool> CheckEmailExistsAsync(string email)
        {
            return dbContext.Users.Where(u => u.Email.Equals(email)).AnyAsync();
        }

        public async Task<bool> CreateAccountAsync(string email, string password,
            UserRole role = UserRole.None)
        {
            password = sha256Helper.Hash(password);

            if (!await CheckEmailExistsAsync(email))
            {
                await dbContext.Users.AddAsync(new User
                {
                    Email = email,
                    Password = password,
                    Role = role
                });
                await dbContext.SaveChangesAsync();

                return true;
            }
            return false;
        }

        public Task<Address> FindDeliveryAddressAsync(int userId)
        {
            return dbContext.Addresses
                .Where(a => a.UserId == userId && a.AddressType == AddressType.DeliveryAddress)
                .FirstOrDefaultAsync();
        }

        public Task<List<Address>> FindUserAddressesAsync(int userId)
        {
            return dbContext.Addresses
                   .Where(a => a.UserId == userId)
                   .Include(a => a.User)
                   .ToListAsync();
        }

        public async Task AddOrUpdateAddressAsync(int userId, 
            Address address, AddressType addressType)
        {
            var addressInDb = dbContext.Addresses
                .Where(a => a.UserId == userId && a.AddressType == addressType).FirstOrDefault();

            if (addressInDb != null)
            {
                addressInDb.Name = address.Name;
                addressInDb.Surname = address.Surname;
                addressInDb.Phone = address.Phone;
                addressInDb.Street = address.Street;
                addressInDb.BuildingNumber = address.BuildingNumber;
                addressInDb.LocalNumber = address.LocalNumber;
                addressInDb.City = address.City;
                addressInDb.PostCode = address.PostCode;

                dbContext.Addresses.Update(addressInDb);
            }
            else
            {
                address.AddressType = addressType;
                address.UserId = userId;

                dbContext.Addresses.Add(address);
            }
            await dbContext.SaveChangesAsync();
        }
    }
}
